namespace LifeManager.Application.Rewards.DTOs
{
    /// <param name="ArchivedAt">Null while the reward is active.</param>
    /// <param name="ExpenseCategoryId">Null without an expense, and after the category was deleted (the amount stays).</param>
    public record RewardResponseDto(
        int Id,
        string Name,
        int Cost,
        string? Icon,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ArchivedAt,
        int? ExpenseCategoryId,
        decimal? ExpenseAmount);
}
