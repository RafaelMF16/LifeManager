namespace LifeManager.Application.Rewards.DTOs
{
    /// <param name="ArchivedAt">Null while the reward is active.</param>
    public record RewardResponseDto(int Id, string Name, int Cost, string? Icon, DateTimeOffset CreatedAt, DateTimeOffset? ArchivedAt);
}
