using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Domain.FinanceDashboard
{
    /// <summary>
    /// How much one category moved in one month for one transaction type: the grain the dashboard is built from.
    /// <see cref="Amount"/> is always positive; <see cref="Type"/> says whether it came in, was spent or was invested.
    /// </summary>
    public record DashboardCategoryMonthTotal(int CategoryId, string CategoryName, MoneyFlowType Type, int Year, int Month, decimal Amount);
}
