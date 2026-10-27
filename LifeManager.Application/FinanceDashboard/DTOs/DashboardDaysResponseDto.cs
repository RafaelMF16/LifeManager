namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>
    /// A month's expenses day by day, for the dashboard's calendar. Days has every day of the month in order;
    /// MaxDayTotal is the biggest day (0 with no expenses), which the calendar's colors are relative to.
    /// MonthlySummaryId is null when the user has no such month, so the frontend can list a day's transactions.
    /// </summary>
    /// <param name="Month">"yyyy-MM".</param>
    public record DashboardDaysResponseDto(string Month, int? MonthlySummaryId, decimal Total, decimal MaxDayTotal, IReadOnlyList<DashboardDayDto> Days);
}
