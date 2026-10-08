using LifeManager.Domain.Rewards;

namespace LifeManager.Application.Rewards.DTOs
{
    /// <summary>One redemption, with the reward's name, icon and price as they were then.</summary>
    /// <param name="RedeemedOn">"yyyy-MM-dd", the game day.</param>
    /// <param name="CanUndo">Whether it can still be undone: not undone, and made today (the backend's today).</param>
    public record RewardRedemptionResponseDto(
        int Id,
        int RewardId,
        string RewardName,
        string? RewardIcon,
        int CostPaid,
        DateOnly RedeemedOn,
        DateTimeOffset RedeemedAt,
        DateTimeOffset? UndoneAt,
        bool CanUndo)
    {
        public static RewardRedemptionResponseDto From(RewardRedemption redemption, DateOnly today)
            => new(
                redemption.Id!.Value,
                redemption.RewardId.Value,
                redemption.RewardName,
                redemption.RewardIcon,
                redemption.CostPaid,
                redemption.RedeemedOn,
                redemption.RedeemedAt,
                redemption.UndoneAt,
                redemption.CanUndo(today));
    }
}
