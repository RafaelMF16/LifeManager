namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>
    /// One month of the period; months with no transactions come with zeros. Balance is what was left in the account,
    /// Saved is income minus expenses (investment included).
    /// </summary>
    public record DashboardMonthDto(int Year, int Month, decimal Income, decimal Expense, decimal Investment, decimal Balance, decimal Saved);
}
