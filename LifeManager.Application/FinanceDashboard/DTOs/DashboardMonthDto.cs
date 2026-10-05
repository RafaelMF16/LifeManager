namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>One month of the period; months with no transactions come with zeros.</summary>
    public record DashboardMonthDto(int Year, int Month, decimal Income, decimal Expense, decimal Investment, decimal Balance);
}
