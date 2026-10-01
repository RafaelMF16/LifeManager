namespace LifeManager.Application.MonthlySummaries.DTOs
{
    /// <summary>Body of <c>POST /api/MonthlySummaries</c>. The year is always the current one, set by the server.</summary>
    public record MonthlySummaryDto(int Month);
}
