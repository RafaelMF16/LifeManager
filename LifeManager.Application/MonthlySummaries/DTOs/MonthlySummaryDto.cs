namespace LifeManager.Application.MonthlySummaries.DTOs
{
    /// <summary>
    /// Body of <c>POST /api/MonthlySummaries</c>. <paramref name="Year"/> is optional: left out, it is the current year
    /// in the business time zone; otherwise it must be the current or the previous one.
    /// </summary>
    public record MonthlySummaryDto(int Month, int? Year = null);
}
