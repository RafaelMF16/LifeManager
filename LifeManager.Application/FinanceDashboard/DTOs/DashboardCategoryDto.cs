namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <param name="Share">Part of the breakdown's total in the period (0–1).</param>
    /// <param name="MonthlyAmounts">One amount per month of the period, in the same order as <see cref="FinanceDashboardResponseDto.Months"/>.</param>
    public record DashboardCategoryDto(
        int CategoryId,
        string Name,
        decimal Amount,
        decimal PreviousAmount,
        decimal? ChangeRatio,
        decimal Share,
        IReadOnlyList<decimal> MonthlyAmounts);
}
