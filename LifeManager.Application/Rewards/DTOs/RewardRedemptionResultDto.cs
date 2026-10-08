using LifeManager.Application.Habits.DTOs;

namespace LifeManager.Application.Rewards.DTOs
{
    /// <summary>What a redemption or its undo changed: the redemption and the player's profile.</summary>
    public record RewardRedemptionResultDto(RewardRedemptionResponseDto Redemption, WalletChangeDto Wallet);
}
