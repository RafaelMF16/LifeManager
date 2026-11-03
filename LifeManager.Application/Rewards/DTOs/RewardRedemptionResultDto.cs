using LifeManager.Application.Habits.DTOs;

namespace LifeManager.Application.Rewards.DTOs
{
    /// <summary>What a redemption or its undo changed: the redemption and the player's profile.</summary>
    /// <param name="MonthlySummaryId">The month the expense went into (or left, on an undo); null without an expense.</param>
    public record RewardRedemptionResultDto(RewardRedemptionResponseDto Redemption, WalletChangeDto Wallet, int? MonthlySummaryId = null);
}
