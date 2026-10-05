using LifeManager.Domain.FinanceDashboard.Enums;

namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>
    /// The period to summarize, as whole months in "yyyy-MM" (both included). The client resolves presets such as
    /// "last 6 months" into From/To itself, since only it knows the user's local date.
    /// </summary>
    public record FinanceDashboardQueryDto
    {
        public string? From { get; init; }
        public string? To { get; init; }
        public DashboardComparison Comparison { get; init; } = DashboardComparison.PreviousPeriod;
    }
}
