namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>Every category beyond the listed ones, summed together.</summary>
    public record DashboardOthersDto(
        int CategoryCount,
        decimal Amount,
        decimal PreviousAmount,
        decimal? ChangeRatio,
        decimal Share,
        IReadOnlyList<decimal> MonthlyAmounts);
}
