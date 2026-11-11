using System.Text.Json.Serialization;
using LifeManager.Domain.SavingsGoals.Enums;

namespace LifeManager.Application.SavingsGoals.DTOs
{
    /// <param name="TargetMonth">"yyyy-MM"; null without a deadline.</param>
    /// <param name="ArchivedAt">Null while the goal is active.</param>
    /// <param name="CompletedAt">The first day the target was reached; kept even if the amount saved drops later.</param>
    /// <param name="Saved">The initial amount plus every linked investment.</param>
    /// <param name="Ratio">Saved ÷ target, 4 places; above 1 once the target is passed.</param>
    /// <param name="Remaining">What is left to save; 0 once reached.</param>
    /// <param name="MonthsLeft">From the current month through the deadline, both included (0 or less once it passed); null without one.</param>
    /// <param name="MonthlyNeeded">Remaining ÷ months left, rounded up to the cent; null without a deadline.</param>
    /// <param name="Monthly">
    /// Only on <c>GET /api/SavingsGoals/{id}</c>: the linked investments of each of the last 12 months, the current one
    /// last. Left out of every other response.
    /// </param>
    public record SavingsGoalResponseDto(
        int Id,
        string Name,
        decimal TargetAmount,
        string? TargetMonth,
        decimal InitialAmount,
        string? Icon,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ArchivedAt,
        DateOnly? CompletedAt,
        decimal Saved,
        decimal Ratio,
        decimal Remaining,
        int? MonthsLeft,
        decimal? MonthlyNeeded,
        SavingsGoalPace Pace,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SavingsGoalMonthDto>? Monthly = null);
}
