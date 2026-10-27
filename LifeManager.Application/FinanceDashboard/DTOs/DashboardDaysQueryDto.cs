namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>The month whose days to sum, in "yyyy-MM".</summary>
    public record DashboardDaysQueryDto
    {
        public string? Month { get; init; }
    }
}
