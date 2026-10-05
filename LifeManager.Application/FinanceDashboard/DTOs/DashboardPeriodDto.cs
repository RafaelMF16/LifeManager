namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <param name="From">First month, "yyyy-MM".</param>
    /// <param name="To">Last month, "yyyy-MM".</param>
    public record DashboardPeriodDto(string From, string To, int MonthCount);
}
