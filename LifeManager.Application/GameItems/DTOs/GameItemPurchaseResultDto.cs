using LifeManager.Application.Habits.DTOs;

namespace LifeManager.Application.GameItems.DTOs
{
    /// <summary>What a purchase or its undo changed: the purchase and the player's profile.</summary>
    public record GameItemPurchaseResultDto(GameItemPurchaseResponseDto Purchase, WalletChangeDto Wallet);
}
