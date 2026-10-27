namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>One day of the month: the expenses summed (0 with none) and how many there were.</summary>
    public record DashboardDayDto(DateOnly Date, decimal Total, int Count);
}
