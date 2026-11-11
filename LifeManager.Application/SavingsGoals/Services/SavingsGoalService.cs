using LifeManager.Application.SavingsGoals.DTOs;
using LifeManager.Application.Shared.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.SavingsGoals;
using LifeManager.Domain.SavingsGoals.Errors;
using LifeManager.Domain.SavingsGoals.Interfaces;
using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.Text;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Transactions.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.SavingsGoals.Services
{
    /// <remarks>
    /// Deleting a goal archives it: its investments keep pointing at it, its name is free for a new goal, and it can be
    /// restored as long as no active goal took that name meanwhile.
    /// </remarks>
    public class SavingsGoalService(
        ISavingsGoalRepository savingsGoalRepository,
        ITransactionRepository transactionRepository,
        SavingsGoalCompletion savingsGoalCompletion,
        AppClock appClock,
        TimeProvider timeProvider)
    {
        /// <summary>How many months, the current one last, the details' chart covers.</summary>
        public const int ChartMonths = 12;

        private readonly ISavingsGoalRepository _savingsGoalRepository = savingsGoalRepository;
        private readonly ITransactionRepository _transactionRepository = transactionRepository;
        private readonly SavingsGoalCompletion _savingsGoalCompletion = savingsGoalCompletion;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<Result<SavingsGoalResponseDto>> CreateAsync(SavingsGoalDto goalDto, UserId userId, CancellationToken cancellationToken)
        {
            if (!TryParseTargetMonth(goalDto.TargetMonth, out var targetMonth))
                return SavingsGoalErrors.InvalidTargetMonth;

            var goalResult = SavingsGoal.Create(
                userId.Value,
                goalDto.Name,
                goalDto.TargetAmount,
                targetMonth,
                goalDto.InitialAmount,
                goalDto.Icon,
                CurrentMonth(),
                _timeProvider.GetUtcNow());
            if (!goalResult.IsSuccess)
                return goalResult.Error;

            var goal = goalResult.Value!;

            if (await _savingsGoalRepository.ExistsActiveByNameAsync(userId, goal.Name, null, cancellationToken))
                return SavingsGoalErrors.NameAlreadyExists;

            await _savingsGoalRepository.AddAsync(goal, cancellationToken);

            // What was already saved may reach the target on its own.
            await _savingsGoalCompletion.RecordIfReachedAsync(goal, SavingsGoalSums.Zero, cancellationToken);

            return ToResponseDto(goal, SavingsGoalSums.Zero);
        }

        public async Task<Result<SavingsGoalResponseDto>> GetByIdAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var goal = await _savingsGoalRepository.GetByIdAsync(new SavingsGoalId(id), userId, cancellationToken);
            if (goal is null)
                return SavingsGoalErrors.NotFound;

            var currentMonth = CurrentMonth();
            var firstChartMonth = currentMonth.AddMonths(-(ChartMonths - 1));
            var sums = await GetSumsAsync(goal, cancellationToken);
            var amounts = await _savingsGoalRepository.GetMonthlyAmountsAsync(goal.Id!, firstChartMonth.FirstDay, cancellationToken);

            var monthly = Enumerable.Range(0, ChartMonths)
                .Select(offset => firstChartMonth.AddMonths(offset))
                .Select(month => new SavingsGoalMonthDto(
                    month.ToString(), amounts.FirstOrDefault(amount => amount.Month == month)?.Amount ?? 0m))
                .ToList();

            return ToResponseDto(goal, sums) with { Monthly = monthly };
        }

        public async Task<Result<PagedResponseDto<SavingsGoalResponseDto>>> GetPagedAsync(
            SavingsGoalListQueryDto query, UserId userId, CancellationToken cancellationToken)
        {
            var pageRequestResult = PageRequest.Create(query.Page, query.PageSize);
            if (!pageRequestResult.IsSuccess)
                return pageRequestResult.Error;

            var pageRequest = pageRequestResult.Value!;
            var normalizedSearch = SearchText.Normalize(query.Search);

            if (normalizedSearch.Length > SavingsGoalName.MaxLength)
                return new PagedResponseDto<SavingsGoalResponseDto>([], 0, pageRequest.Page, pageRequest.PageSize, 0);

            var goals = await _savingsGoalRepository.GetPagedByUserIdAsync(
                userId, pageRequest, query.Status, normalizedSearch, query.SortBy, query.SortDirection, cancellationToken);

            var sums = await _savingsGoalRepository.GetLinkedSumsAsync([.. goals.Items.Select(goal => goal.Id!)], RecentFrom(), cancellationToken);

            return PagedResponseDto<SavingsGoalResponseDto>.From(goals, goal => ToResponseDto(goal, sums.GetValueOrDefault(goal.Id!, SavingsGoalSums.Zero)));
        }

        /// <summary>The goal's transactions across every month, newest first; archived goals included.</summary>
        public async Task<Result<PagedResponseDto<SavingsGoalTransactionDto>>> GetTransactionsAsync(
            int id, SavingsGoalTransactionListQueryDto query, UserId userId, CancellationToken cancellationToken)
        {
            var pageRequestResult = PageRequest.Create(query.Page, query.PageSize);
            if (!pageRequestResult.IsSuccess)
                return pageRequestResult.Error;

            var goal = await _savingsGoalRepository.GetByIdAsync(new SavingsGoalId(id), userId, cancellationToken);
            if (goal is null)
                return SavingsGoalErrors.NotFound;

            var transactions = await _transactionRepository.GetPagedBySavingsGoalAsync(goal.Id!, pageRequestResult.Value!, cancellationToken);

            return PagedResponseDto<SavingsGoalTransactionDto>.From(transactions, item => new SavingsGoalTransactionDto(
                item.Transaction.Id!.Value,
                item.Transaction.MonthlySummaryId.Value,
                item.Transaction.CategoryId.Value,
                item.CategoryName,
                item.Transaction.Amount.Value,
                item.Transaction.Description.Value,
                item.Transaction.TransactionDate));
        }

        public async Task<Result<SavingsGoalResponseDto>> UpdateAsync(int id, SavingsGoalDto goalDto, UserId userId, CancellationToken cancellationToken)
        {
            var goal = await _savingsGoalRepository.GetByIdAsync(new SavingsGoalId(id), userId, cancellationToken);
            if (goal is null)
                return SavingsGoalErrors.NotFound;

            if (!TryParseTargetMonth(goalDto.TargetMonth, out var targetMonth))
                return SavingsGoalErrors.InvalidTargetMonth;

            var currentNormalizedName = goal.NormalizedName;

            var updateResult = goal.Update(goalDto.Name, goalDto.TargetAmount, targetMonth, goalDto.InitialAmount, goalDto.Icon, CurrentMonth());
            if (!updateResult.IsSuccess)
                return updateResult.Error;

            if (goal.NormalizedName != currentNormalizedName
                && await _savingsGoalRepository.ExistsActiveByNameAsync(userId, goal.Name, goal.Id, cancellationToken))
                return SavingsGoalErrors.NameAlreadyExists;

            await _savingsGoalRepository.UpdateAsync(goal, cancellationToken);

            // A lower target or a higher initial amount may reach it.
            var sums = await GetSumsAsync(goal, cancellationToken);
            await _savingsGoalCompletion.RecordIfReachedAsync(goal, sums, cancellationToken);

            return ToResponseDto(goal, sums);
        }

        /// <summary>Archives the goal; its investments keep pointing at it.</summary>
        public async Task<Result> ArchiveAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var goal = await _savingsGoalRepository.GetByIdAsync(new SavingsGoalId(id), userId, cancellationToken);
            if (goal is null)
                return SavingsGoalErrors.NotFound;

            var archiveResult = goal.Archive(_timeProvider.GetUtcNow());
            if (!archiveResult.IsSuccess)
                return archiveResult.Error;

            await _savingsGoalRepository.UpdateAsync(goal, cancellationToken);

            return Result.Success();
        }

        public async Task<Result<SavingsGoalResponseDto>> RestoreAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var goal = await _savingsGoalRepository.GetByIdAsync(new SavingsGoalId(id), userId, cancellationToken);
            if (goal is null)
                return SavingsGoalErrors.NotFound;

            var restoreResult = goal.Restore();
            if (!restoreResult.IsSuccess)
                return restoreResult.Error;

            if (await _savingsGoalRepository.ExistsActiveByNameAsync(userId, goal.Name, goal.Id, cancellationToken))
                return SavingsGoalErrors.NameAlreadyExists;

            await _savingsGoalRepository.UpdateAsync(goal, cancellationToken);

            return ToResponseDto(goal, await GetSumsAsync(goal, cancellationToken));
        }

        private YearMonth CurrentMonth() => YearMonth.From(_appClock.Today());

        /// <summary>The first day of the pace window: the current month and the ones before it.</summary>
        private DateOnly RecentFrom() => CurrentMonth().AddMonths(-(SavingsGoalProgress.PaceWindowMonths - 1)).FirstDay;

        private async Task<SavingsGoalSums> GetSumsAsync(SavingsGoal goal, CancellationToken cancellationToken)
        {
            var sums = await _savingsGoalRepository.GetLinkedSumsAsync([goal.Id!], RecentFrom(), cancellationToken);

            return sums.GetValueOrDefault(goal.Id!, SavingsGoalSums.Zero);
        }

        /// <summary>Null or blank means no deadline; anything else must be "yyyy-MM".</summary>
        private static bool TryParseTargetMonth(string? value, out YearMonth? targetMonth)
        {
            targetMonth = null;
            if (string.IsNullOrWhiteSpace(value))
                return true;

            if (!YearMonth.TryParse(value.Trim(), out var month))
                return false;

            targetMonth = month;
            return true;
        }

        private SavingsGoalResponseDto ToResponseDto(SavingsGoal goal, SavingsGoalSums sums)
        {
            var progress = SavingsGoalProgress.Calculate(
                goal.TargetAmount, goal.InitialAmount, sums.Total, goal.TargetMonth, CurrentMonth(), sums.Recent);

            return new(
                goal.Id!.Value,
                goal.Name.Value,
                goal.TargetAmount,
                goal.TargetMonth?.ToString(),
                goal.InitialAmount,
                goal.Icon,
                goal.CreatedAt,
                goal.ArchivedAt,
                goal.CompletedAt,
                progress.Saved,
                progress.Ratio,
                progress.Remaining,
                progress.MonthsLeft,
                progress.MonthlyNeeded,
                progress.Pace);
        }
    }
}
