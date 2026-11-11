using LifeManager.Application.Habits.Services;
using LifeManager.Application.Shared.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Application.Transactions.DTOs;
using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.Categories.Interfaces;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.Errors;
using LifeManager.Domain.MonthlySummaries.Interfaces;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.SavingsGoals.Errors;
using LifeManager.Domain.SavingsGoals.Interfaces;
using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.Text;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Transactions.Errors;
using LifeManager.Domain.Transactions.Interfaces;
using LifeManager.Domain.Transactions.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Transactions.Services
{
    /// <remarks>
    /// Every operation first checks that the month belongs to the user, then works only inside that month,
    /// so a transaction is never reachable through another user's month.
    /// </remarks>
    public class TransactionService(
        ITransactionRepository transactionRepository,
        IMonthlySummaryRepository monthlySummaryRepository,
        ICategoryRepository categoryRepository,
        ISavingsGoalRepository savingsGoalRepository,
        HabitAutoCheckInService habitAutoCheckInService,
        AppClock appClock)
    {
        private readonly ITransactionRepository _transactionRepository = transactionRepository;
        private readonly IMonthlySummaryRepository _monthlySummaryRepository = monthlySummaryRepository;
        private readonly ICategoryRepository _categoryRepository = categoryRepository;
        private readonly ISavingsGoalRepository _savingsGoalRepository = savingsGoalRepository;
        private readonly HabitAutoCheckInService _habitAutoCheckInService = habitAutoCheckInService;
        private readonly AppClock _appClock = appClock;

        public async Task<Result<TransactionResponseDto>> CreateAsync(int monthlySummaryId, TransactionDto transactionDto, UserId userId, CancellationToken cancellationToken)
        {
            var monthlySummary = await _monthlySummaryRepository.GetByIdAsync(new MonthlySummaryId(monthlySummaryId), userId, cancellationToken);
            if (monthlySummary is null)
                return MonthlySummaryErrors.NotFound;

            var transactionResult = Transaction.Create(
                transactionDto.Type,
                transactionDto.CategoryId,
                transactionDto.Amount,
                transactionDto.Description,
                transactionDto.Date,
                monthlySummary,
                transactionDto.SavingsGoalId);
            if (!transactionResult.IsSuccess)
                return transactionResult.Error;

            var category = await _categoryRepository.GetByIdAsync(new CategoryId(transactionDto.CategoryId), userId, cancellationToken);
            if (category is null)
                return CategoryErrors.NotFound;

            var goalResult = await EnsureGoalCanTakeAsync(transactionDto.SavingsGoalId, null, userId, cancellationToken);
            if (!goalResult.IsSuccess)
                return goalResult.Error;

            var transaction = transactionResult.Value;
            await _transactionRepository.AddAsync(transaction, cancellationToken);

            // Only a transaction typed by hand: recurrences, imports and reward expenses don't come through here.
            var autoCheckIns = await _habitAutoCheckInService.OnEventAsync(userId, HabitAutoTrigger.TransactionCreated, cancellationToken);

            return ToResponseDto(transaction, category.Name.Value) with { AutoCheckIns = autoCheckIns };
        }

        /// <summary>
        /// Creates a transaction without picking its month first: it falls into the user's month of its date, of any
        /// year, and a missing month is opened when it is in the current or the previous year. Every rule is checked
        /// before anything is written, so an invalid transaction never leaves an empty month behind.
        /// </summary>
        public async Task<Result<QuickTransactionResponseDto>> CreateQuickAsync(TransactionDto transactionDto, UserId userId, CancellationToken cancellationToken)
        {
            var period = YearMonth.From(transactionDto.Date);
            var monthlySummary = await _monthlySummaryRepository.GetByPeriodAsync(userId, period, cancellationToken);
            var monthCreated = monthlySummary is null;

            if (monthlySummary is null)
            {
                var openResult = MonthlySummary.OpenForEntry(userId, period, _appClock.Today().Year);
                if (!openResult.IsSuccess)
                    return openResult.Error;

                monthlySummary = openResult.Value;
            }

            var transactionResult = BuildTransaction(transactionDto, monthlySummary);
            if (!transactionResult.IsSuccess)
                return transactionResult.Error;

            var category = await _categoryRepository.GetByIdAsync(new CategoryId(transactionDto.CategoryId), userId, cancellationToken);
            if (category is null)
                return CategoryErrors.NotFound;

            var goalResult = await EnsureGoalCanTakeAsync(transactionDto.SavingsGoalId, null, userId, cancellationToken);
            if (!goalResult.IsSuccess)
                return goalResult.Error;

            if (monthCreated)
            {
                // Validated against the unsaved month; built again with the stored one, which now has its id.
                monthlySummary = await _monthlySummaryRepository.AddIfMissingAsync(monthlySummary, cancellationToken);
                transactionResult = BuildTransaction(transactionDto, monthlySummary);
            }

            var transaction = transactionResult.Value!;
            await _transactionRepository.AddAsync(transaction, cancellationToken);

            var autoCheckIns = await _habitAutoCheckInService.OnEventAsync(userId, HabitAutoTrigger.TransactionCreated, cancellationToken);

            var created = ToResponseDto(transaction, category.Name.Value);

            return new QuickTransactionResponseDto(
                created.Id,
                created.Type,
                created.CategoryId,
                created.CategoryName,
                created.Amount,
                created.Description,
                created.Date,
                created.RecurringTransactionId,
                monthlySummary.Id!.Value,
                monthCreated,
                autoCheckIns,
                created.SavingsGoalId);
        }

        public async Task<Result<TransactionResponseDto>> GetByIdAsync(int monthlySummaryId, int id, UserId userId, CancellationToken cancellationToken)
        {
            var monthlySummary = await _monthlySummaryRepository.GetByIdAsync(new MonthlySummaryId(monthlySummaryId), userId, cancellationToken);
            if (monthlySummary is null)
                return MonthlySummaryErrors.NotFound;

            var item = await _transactionRepository.GetByIdAsync(new TransactionId(id), monthlySummary.Id!, cancellationToken);
            if (item is null)
                return TransactionErrors.NotFound;

            return ToResponseDto(item);
        }

        public async Task<Result<PagedResponseDto<TransactionResponseDto>>> GetPagedAsync(int monthlySummaryId, TransactionListQueryDto query, UserId userId, CancellationToken cancellationToken)
        {
            var pageRequestResult = PageRequest.Create(query.Page, query.PageSize);
            if (!pageRequestResult.IsSuccess)
                return pageRequestResult.Error;

            var pageRequest = pageRequestResult.Value;

            var monthlySummary = await _monthlySummaryRepository.GetByIdAsync(new MonthlySummaryId(monthlySummaryId), userId, cancellationToken);
            if (monthlySummary is null)
                return MonthlySummaryErrors.NotFound;

            if (query.Date is { } date && (date.Year != monthlySummary.Year.Value || date.Month != monthlySummary.Month.Value))
                return TransactionErrors.DateOutsideMonth;

            var normalizedSearch = SearchText.Normalize(query.Search);

            if (normalizedSearch.Length > TransactionDescription.MaxLength)
                return new PagedResponseDto<TransactionResponseDto>([], 0, pageRequest.Page, pageRequest.PageSize, 0);

            var categoryId = query.CategoryId is null ? null : new CategoryId(query.CategoryId.Value);

            var savingsGoalId = query.SavingsGoalId is null ? null : new SavingsGoalId(query.SavingsGoalId.Value);

            var transactions = await _transactionRepository.GetPagedAsync(
                monthlySummary.Id!, pageRequest, query.Type, categoryId, query.Date, savingsGoalId, normalizedSearch, query.SortBy, query.SortDirection, cancellationToken);

            return PagedResponseDto<TransactionResponseDto>.From(transactions, ToResponseDto);
        }

        public async Task<Result<TransactionResponseDto>> UpdateAsync(int monthlySummaryId, int id, TransactionDto transactionDto, UserId userId, CancellationToken cancellationToken)
        {
            var monthlySummary = await _monthlySummaryRepository.GetByIdAsync(new MonthlySummaryId(monthlySummaryId), userId, cancellationToken);
            if (monthlySummary is null)
                return MonthlySummaryErrors.NotFound;

            var item = await _transactionRepository.GetByIdAsync(new TransactionId(id), monthlySummary.Id!, cancellationToken);
            if (item is null)
                return TransactionErrors.NotFound;

            var transaction = item.Transaction;
            var currentGoalId = transaction.SavingsGoalId;

            var updateResult = transaction.Update(
                transactionDto.Type,
                transactionDto.CategoryId,
                transactionDto.Amount,
                transactionDto.Description,
                transactionDto.Date,
                monthlySummary,
                transactionDto.SavingsGoalId);
            if (!updateResult.IsSuccess)
                return updateResult.Error;

            var category = await _categoryRepository.GetByIdAsync(transaction.CategoryId, userId, cancellationToken);
            if (category is null)
                return CategoryErrors.NotFound;

            var goalResult = await EnsureGoalCanTakeAsync(transactionDto.SavingsGoalId, currentGoalId, userId, cancellationToken);
            if (!goalResult.IsSuccess)
                return goalResult.Error;

            await _transactionRepository.UpdateAsync(transaction, cancellationToken);

            return ToResponseDto(transaction, category.Name.Value);
        }

        public async Task<Result> DeleteAsync(int monthlySummaryId, int id, UserId userId, CancellationToken cancellationToken)
        {
            var monthlySummary = await _monthlySummaryRepository.GetByIdAsync(new MonthlySummaryId(monthlySummaryId), userId, cancellationToken);
            if (monthlySummary is null)
                return MonthlySummaryErrors.NotFound;

            var deleted = await _transactionRepository.DeleteAsync(new TransactionId(id), monthlySummary.Id!, cancellationToken);
            if (!deleted)
                return TransactionErrors.NotFound;

            return Result.Success();
        }

        private static Result<Transaction> BuildTransaction(TransactionDto transactionDto, MonthlySummary monthlySummary)
            => Transaction.Create(
                transactionDto.Type,
                transactionDto.CategoryId,
                transactionDto.Amount,
                transactionDto.Description,
                transactionDto.Date,
                monthlySummary,
                transactionDto.SavingsGoalId);

        /// <summary>
        /// A goal being linked must be the user's (<c>SavingsGoal.NotFound</c>) and active (<c>SavingsGoal.Archived</c>).
        /// Keeping the goal the transaction already had is always allowed, so a transaction of an archived goal can
        /// still be edited.
        /// </summary>
        private async Task<Result> EnsureGoalCanTakeAsync(int? savingsGoalId, SavingsGoalId? currentGoalId, UserId userId, CancellationToken cancellationToken)
        {
            if (savingsGoalId is null || savingsGoalId == currentGoalId?.Value)
                return Result.Success();

            var goal = await _savingsGoalRepository.GetByIdAsync(new SavingsGoalId(savingsGoalId.Value), userId, cancellationToken);
            if (goal is null)
                return SavingsGoalErrors.NotFound;

            return goal.EnsureCanLink();
        }

        private static TransactionResponseDto ToResponseDto(TransactionListItem item)
            => ToResponseDto(item.Transaction, item.CategoryName);

        private static TransactionResponseDto ToResponseDto(Transaction transaction, string categoryName)
            => new(
                transaction.Id!.Value,
                transaction.Type,
                transaction.CategoryId.Value,
                categoryName,
                transaction.Amount.Value,
                transaction.Description.Value,
                transaction.TransactionDate,
                transaction.RecurringTransactionId?.Value,
                transaction.SavingsGoalId?.Value);
    }
}
