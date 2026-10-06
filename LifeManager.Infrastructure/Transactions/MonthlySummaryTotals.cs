using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Transactions
{
    /// <summary>
    /// Keeps a month's totals equal to the sum of its transactions. Every write to a month's transactions runs, inside
    /// one database transaction opened by the caller, as: <see cref="LockAsync"/>, the write, <see cref="RecalculateAsync"/>.
    /// The lock serializes concurrent writes to the same month, so the last recalculation always sees every committed
    /// transaction; summing (instead of adding deltas) means the totals can't drift.
    /// </summary>
    internal static class MonthlySummaryTotals
    {
        /// <summary>Locks the month's row (SELECT ... FOR UPDATE) until the caller's database transaction ends.</summary>
        public static async Task<MonthlySummary> LockAsync(LifeManagerDbContext dbContext, MonthlySummaryId monthlySummaryId, CancellationToken cancellationToken)
        {
            var id = monthlySummaryId.Value;
            var lockedSummaries = await dbContext.MonthlySummaries
                .FromSql($"""SELECT * FROM "MonthlySummaries" WHERE "Id" = {id} FOR UPDATE""")
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return lockedSummaries.Single();
        }

        /// <summary>Sums the month's transactions per type and stores the totals and balance on the locked month.</summary>
        public static async Task RecalculateAsync(LifeManagerDbContext dbContext, MonthlySummary lockedSummary, CancellationToken cancellationToken)
        {
            var monthlySummaryId = lockedSummary.Id!;

            var totals = await dbContext.Transactions
                .Where(transaction => transaction.MonthlySummaryId == monthlySummaryId)
                .GroupBy(transaction => transaction.Type)
                .Select(group => new { Type = group.Key, Total = group.Sum(transaction => transaction.SignedAmount) })
                .ToListAsync(cancellationToken);

            var totalIncome = totals.Where(total => total.Type == MoneyFlowType.Income).Sum(total => total.Total);
            var totalExpense = -totals.Where(total => total.Type == MoneyFlowType.Expense).Sum(total => total.Total);
            var totalInvestment = -totals.Where(total => total.Type == MoneyFlowType.Investment).Sum(total => total.Total);

            var applyResult = lockedSummary.ApplyTotals(totalIncome, totalExpense, totalInvestment);
            if (!applyResult.IsSuccess)
                throw new InvalidOperationException($"Recalculated totals are invalid: {applyResult.Error.Code}");

            await dbContext.MonthlySummaries
                .Where(storedSummary => storedSummary.Id == monthlySummaryId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(storedSummary => storedSummary.TotalIncome, lockedSummary.TotalIncome)
                    .SetProperty(storedSummary => storedSummary.TotalExpense, lockedSummary.TotalExpense)
                    .SetProperty(storedSummary => storedSummary.TotalInvestment, lockedSummary.TotalInvestment)
                    .SetProperty(storedSummary => storedSummary.BalanceAmount, lockedSummary.BalanceAmount), cancellationToken);
        }
    }
}
