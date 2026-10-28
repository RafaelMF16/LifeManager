using LifeManager.Domain.GameItems;
using LifeManager.Domain.GameItems.Enums;

namespace LifeManager.Application.GameItems.DTOs
{
    /// <summary>One item purchase, with the price paid and the HP actually healed.</summary>
    /// <param name="PurchasedOn">"yyyy-MM-dd", the game day.</param>
    /// <param name="CanUndo">Whether it can still be undone: not undone, and made today (the backend's today).</param>
    public record GameItemPurchaseResponseDto(
        int Id,
        GameItemKind Kind,
        int CostPaid,
        int HpRestored,
        DateOnly PurchasedOn,
        DateTimeOffset PurchasedAt,
        DateTimeOffset? UndoneAt,
        bool IsUndone,
        bool CanUndo)
    {
        public static GameItemPurchaseResponseDto From(GameItemPurchase purchase, DateOnly today)
            => new(
                purchase.Id!.Value,
                purchase.Kind,
                purchase.CostPaid,
                purchase.HpRestored,
                purchase.PurchasedOn,
                purchase.PurchasedAt,
                purchase.UndoneAt,
                purchase.IsUndone,
                purchase.CanUndo(today));
    }
}
