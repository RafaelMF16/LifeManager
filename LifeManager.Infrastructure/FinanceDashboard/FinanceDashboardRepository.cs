using LifeManager.Domain.FinanceDashboard;
using LifeManager.Domain.FinanceDashboard.Interfaces;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.FinanceDashboard
{
    public class FinanceDashboardRepository(LifeManagerDbContext dbContext) : IFinanceDashboardRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<IReadOnlyList<DashboardCategoryMonthTotal>> GetCategoryMonthTotalsAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            // Transactions have no UserId: ownership comes from their month. The date range is on the plain
            // TransactionDate column (indexed with MonthlySummaryId); the month's Year/Month are converted value
            // objects and can't be range-compared in SQL. Sums use SignedAmount, since Amount is converted too.
            var totals = await _dbContext.Transactions
                .AsNoTracking()
                .Where(transaction => transaction.TransactionDate >= from
                    && transaction.TransactionDate <= to
                    && _dbContext.MonthlySummaries.Any(monthlySummary => monthlySummary.Id == transaction.MonthlySummaryId && monthlySummary.UserId == userId))
                .GroupBy(transaction => new
                {
                    transaction.CategoryId,
                    transaction.Type,
                    transaction.TransactionDate.Year,
                    transaction.TransactionDate.Month
                })
                .Select(group => new
                {
                    group.Key.CategoryId,
                    group.Key.Type,
                    group.Key.Year,
                    group.Key.Month,
                    Total = group.Sum(transaction => transaction.SignedAmount)
                })
                .ToListAsync(cancellationToken);

            if (totals.Count == 0)
                return [];

            // Names are read on their own, from the user's categories: grouping by the converted Name column
            // isn't something EF translates reliably, and a user's category list is small.
            var categoryNames = await _dbContext.Categories
                .AsNoTracking()
                .Where(category => category.UserId == userId)
                .Select(category => new { category.Id, category.Name })
                .ToListAsync(cancellationToken);
            var namesById = categoryNames.ToDictionary(category => category.Id!.Value, category => category.Name.Value);

            return [.. totals.Select(total => new DashboardCategoryMonthTotal(
                total.CategoryId.Value,
                namesById.GetValueOrDefault(total.CategoryId.Value, string.Empty),
                total.Type,
                total.Year,
                total.Month,
                total.Type == MoneyFlowType.Income ? total.Total : -total.Total))];
        }
    }
}
