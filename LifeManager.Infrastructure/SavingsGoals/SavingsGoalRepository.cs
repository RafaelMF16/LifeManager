using LifeManager.Domain.SavingsGoals;
using LifeManager.Domain.SavingsGoals.Enums;
using LifeManager.Domain.SavingsGoals.Interfaces;
using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.SavingsGoals
{
    public class SavingsGoalRepository(LifeManagerDbContext dbContext) : ISavingsGoalRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<SavingsGoal> AddAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken)
        {
            _dbContext.Add(savingsGoal);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return savingsGoal;
        }

        public async Task<SavingsGoal?> GetByIdAsync(SavingsGoalId savingsGoalId, UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.SavingsGoals
                .AsNoTracking()
                .SingleOrDefaultAsync(goal => goal.Id == savingsGoalId && goal.UserId == userId, cancellationToken);
        }

        public async Task<PagedList<SavingsGoal>> GetPagedByUserIdAsync(
            UserId userId,
            PageRequest pageRequest,
            SavingsGoalStatusFilter statusFilter,
            string? normalizedSearch,
            SavingsGoalSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken)
        {
            var query = _dbContext.SavingsGoals
                .AsNoTracking()
                .Where(goal => goal.UserId == userId);

            query = statusFilter switch
            {
                SavingsGoalStatusFilter.Archived => query.Where(goal => goal.ArchivedAt != null),
                SavingsGoalStatusFilter.Completed => query.Where(goal => goal.ArchivedAt == null && goal.CompletedAt != null),
                _ => query.Where(goal => goal.ArchivedAt == null && goal.CompletedAt == null)
            };

            if (!string.IsNullOrEmpty(normalizedSearch))
            {
                var pattern = QueryablePagingExtensions.ToContainsLikePattern(normalizedSearch);
                query = query.Where(goal => EF.Functions.Like(goal.NormalizedName, pattern, QueryablePagingExtensions.LikeEscapeCharacter));
            }

            return await Order(query, sortBy, sortDirection).ToPagedListAsync(pageRequest, cancellationToken);
        }

        public async Task<IReadOnlyDictionary<SavingsGoalId, SavingsGoalSums>> GetLinkedSumsAsync(
            IReadOnlyCollection<SavingsGoalId> savingsGoalIds, DateOnly recentFrom, CancellationToken cancellationToken)
        {
            if (savingsGoalIds.Count == 0)
                return new Dictionary<SavingsGoalId, SavingsGoalSums>();

            // An investment's SignedAmount is negative (money leaving the account); EF can't sum the Amount value object.
            var rows = await _dbContext.Transactions
                .AsNoTracking()
                .Where(transaction => transaction.SavingsGoalId != null
                    && savingsGoalIds.Contains(transaction.SavingsGoalId)
                    && transaction.Type == MoneyFlowType.Investment)
                .GroupBy(transaction => transaction.SavingsGoalId)
                .Select(group => new
                {
                    SavingsGoalId = group.Key,
                    Total = group.Sum(transaction => -transaction.SignedAmount),
                    Recent = group.Where(transaction => transaction.TransactionDate >= recentFrom).Sum(transaction => -transaction.SignedAmount)
                })
                .ToListAsync(cancellationToken);

            return rows.ToDictionary(row => row.SavingsGoalId!, row => new SavingsGoalSums(row.Total, row.Recent));
        }

        public async Task<IReadOnlyList<SavingsGoalMonthAmount>> GetMonthlyAmountsAsync(SavingsGoalId savingsGoalId, DateOnly from, CancellationToken cancellationToken)
        {
            var rows = await _dbContext.Transactions
                .AsNoTracking()
                .Where(transaction => transaction.SavingsGoalId == savingsGoalId
                    && transaction.Type == MoneyFlowType.Investment
                    && transaction.TransactionDate >= from)
                .GroupBy(transaction => new { transaction.TransactionDate.Year, transaction.TransactionDate.Month })
                .Select(group => new { group.Key.Year, group.Key.Month, Amount = group.Sum(transaction => -transaction.SignedAmount) })
                .ToListAsync(cancellationToken);

            return [.. rows
                .Select(row => new SavingsGoalMonthAmount(YearMonth.From(new DateOnly(row.Year, row.Month, 1)), row.Amount))
                .OrderBy(row => row.Month.Ordinal)];
        }

        public async Task<bool> ExistsActiveByNameAsync(UserId userId, SavingsGoalName name, SavingsGoalId? ignoredSavingsGoalId, CancellationToken cancellationToken)
        {
            var normalizedName = name.NormalizedValue;
            var query = _dbContext.SavingsGoals
                .Where(goal => goal.UserId == userId && goal.NormalizedName == normalizedName && goal.ArchivedAt == null);

            if (ignoredSavingsGoalId is not null)
                query = query.Where(goal => goal.Id != ignoredSavingsGoalId);

            return await query.AnyAsync(cancellationToken);
        }

        public async Task MarkCompletedAsync(SavingsGoalId savingsGoalId, DateOnly date, CancellationToken cancellationToken)
        {
            // Conditional, so two writes reaching the target at once keep the first date.
            await _dbContext.SavingsGoals
                .Where(goal => goal.Id == savingsGoalId && goal.CompletedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(goal => goal.CompletedAt, date), cancellationToken);
        }

        public async Task UpdateAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken)
        {
            await _dbContext.SavingsGoals
                .Where(storedGoal => storedGoal.Id == savingsGoal.Id && storedGoal.UserId == savingsGoal.UserId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(storedGoal => storedGoal.Name, savingsGoal.Name)
                    .SetProperty(storedGoal => storedGoal.NormalizedName, savingsGoal.NormalizedName)
                    .SetProperty(storedGoal => storedGoal.TargetAmount, savingsGoal.TargetAmount)
                    .SetProperty(storedGoal => storedGoal.TargetMonthStart, savingsGoal.TargetMonthStart)
                    .SetProperty(storedGoal => storedGoal.InitialAmount, savingsGoal.InitialAmount)
                    .SetProperty(storedGoal => storedGoal.Icon, savingsGoal.Icon)
                    .SetProperty(storedGoal => storedGoal.ArchivedAt, savingsGoal.ArchivedAt), cancellationToken);
        }

        /// <summary>Sorts by the chosen column, then by name, ending with Id so pages are stable.</summary>
        private IOrderedQueryable<SavingsGoal> Order(IQueryable<SavingsGoal> query, SavingsGoalSortBy sortBy, SortDirection sortDirection)
        {
            var transactions = _dbContext.Transactions;

            var descending = sortDirection == SortDirection.Desc;

            var ordered = sortBy switch
            {
                // A correlated SUM per goal: (initial + linked investments) / target, the ratio SavingsGoalProgress reports.
                SavingsGoalSortBy.Progress => descending
                    ? query.OrderByDescending(goal => (goal.InitialAmount + transactions
                        .Where(transaction => transaction.SavingsGoalId == goal.Id && transaction.Type == MoneyFlowType.Investment)
                        .Sum(transaction => -transaction.SignedAmount)) / goal.TargetAmount)
                    : query.OrderBy(goal => (goal.InitialAmount + transactions
                        .Where(transaction => transaction.SavingsGoalId == goal.Id && transaction.Type == MoneyFlowType.Investment)
                        .Sum(transaction => -transaction.SignedAmount)) / goal.TargetAmount),
                SavingsGoalSortBy.TargetMonth => descending
                    ? query.OrderByDescending(goal => goal.TargetMonthStart)
                    : query.OrderBy(goal => goal.TargetMonthStart),
                _ => descending
                    ? query.OrderByDescending(goal => goal.NormalizedName)
                    : query.OrderBy(goal => goal.NormalizedName)
            };

            return descending
                ? ordered.ThenByDescending(goal => goal.NormalizedName).ThenByDescending(goal => goal.Id)
                : ordered.ThenBy(goal => goal.NormalizedName).ThenBy(goal => goal.Id);
        }
    }
}
