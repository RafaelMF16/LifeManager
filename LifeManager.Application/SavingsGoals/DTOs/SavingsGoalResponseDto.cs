namespace LifeManager.Application.SavingsGoals.DTOs
{
    /// <param name="TargetMonth">"yyyy-MM"; null without a deadline.</param>
    /// <param name="ArchivedAt">Null while the goal is active.</param>
    /// <param name="CompletedAt">The first day the target was reached; kept even if the amount saved drops later.</param>
    public record SavingsGoalResponseDto(
        int Id,
        string Name,
        decimal TargetAmount,
        string? TargetMonth,
        decimal InitialAmount,
        string? Icon,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ArchivedAt,
        DateOnly? CompletedAt);
}
