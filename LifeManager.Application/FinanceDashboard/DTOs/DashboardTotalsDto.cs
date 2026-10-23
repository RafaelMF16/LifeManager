namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>
    /// The period's totals. Balance is what was left in the account (investment taken out); Saved is income minus
    /// expenses, investment included, and SavingsRate its part of the income (current period only, null with no income).
    /// </summary>
    public record DashboardTotalsDto(
        DashboardAmountDto Income,
        DashboardAmountDto Expense,
        DashboardAmountDto Investment,
        DashboardAmountDto Balance,
        DashboardAmountDto Saved,
        decimal? SavingsRate);
}
