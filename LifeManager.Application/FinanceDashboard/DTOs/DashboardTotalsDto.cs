namespace LifeManager.Application.FinanceDashboard.DTOs
{
    public record DashboardTotalsDto(DashboardAmountDto Income, DashboardAmountDto Expense, DashboardAmountDto Investment, DashboardAmountDto Balance);
}
