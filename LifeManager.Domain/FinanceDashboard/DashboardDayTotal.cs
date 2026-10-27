namespace LifeManager.Domain.FinanceDashboard
{
    /// <summary>What was spent on one day: the summed expenses (always positive) and how many there were.</summary>
    public record DashboardDayTotal(DateOnly Date, decimal Amount, int Count);
}
