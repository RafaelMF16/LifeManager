namespace LifeManager.Application.GameItems.DTOs
{
    /// <summary>The shop's items for the player, and today's purchases (the ones an undo can still reach).</summary>
    public record GameItemsResponseDto(IReadOnlyList<GameItemDto> Items, IReadOnlyList<GameItemPurchaseResponseDto> TodayPurchases);
}
