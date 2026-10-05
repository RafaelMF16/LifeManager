namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>Categories of one transaction type, biggest first; <see cref="Others"/> is null when every category is listed.</summary>
    public record DashboardCategoryBreakdownDto(
        decimal Total,
        decimal PreviousTotal,
        decimal? ChangeRatio,
        IReadOnlyList<DashboardCategoryDto> Items,
        DashboardOthersDto? Others);
}
