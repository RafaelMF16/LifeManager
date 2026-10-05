namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>
    /// Everything the Finance dashboard shows for a period, already summed and compared on the server.
    /// Amounts are always positive except balances; ratios are 0–1 (0.12 = 12%) and null when there is nothing to compare with.
    /// </summary>
    public record FinanceDashboardResponseDto(
        DashboardPeriodDto Period,
        DashboardPeriodDto ComparisonPeriod,
        DashboardTotalsDto Totals,
        IReadOnlyList<DashboardMonthDto> Months,
        DashboardCategoryBreakdownDto Expenses,
        DashboardCategoryBreakdownDto Investments);
}
