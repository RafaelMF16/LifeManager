using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.FinanceDashboard;
using LifeManager.Domain.FinanceDashboard.Interfaces;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.FinanceDashboard.Mocks
{
    public class FinanceDashboardRepositoryMock : IFinanceDashboardRepository
    {
        // Mirrors FinanceDashboardRepository: the user's transactions (through their month) in the inclusive date
        // range, summed per category, type and month, with the same sign rule and category-name lookup.
        public Task<IReadOnlyList<DashboardCategoryMonthTotal>> GetCategoryMonthTotalsAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            var namesById = CategorySingleton.Instance
                .Where(category => category.UserId == userId)
                .ToDictionary(category => category.Id!.Value, category => category.Name.Value);

            IReadOnlyList<DashboardCategoryMonthTotal> totals = [.. TransactionSingleton.Instance
                .Where(transaction => transaction.TransactionDate >= from
                    && transaction.TransactionDate <= to
                    && MonthlySummarySingleton.Instance.Any(monthlySummary => monthlySummary.Id == transaction.MonthlySummaryId && monthlySummary.UserId == userId))
                .GroupBy(transaction => new
                {
                    CategoryId = transaction.CategoryId.Value,
                    transaction.Type,
                    transaction.TransactionDate.Year,
                    transaction.TransactionDate.Month
                })
                .Select(group =>
                {
                    var total = group.Sum(transaction => transaction.SignedAmount);

                    return new DashboardCategoryMonthTotal(
                        group.Key.CategoryId,
                        namesById.GetValueOrDefault(group.Key.CategoryId, string.Empty),
                        group.Key.Type,
                        group.Key.Year,
                        group.Key.Month,
                        group.Key.Type == MoneyFlowType.Income ? total : -total);
                })];

            return Task.FromResult(totals);
        }
    }
}
