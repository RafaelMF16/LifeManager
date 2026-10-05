namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>One figure in the period and in the comparison period.</summary>
    public record DashboardAmountDto(decimal Current, decimal Previous, decimal Difference, decimal? ChangeRatio);
}
