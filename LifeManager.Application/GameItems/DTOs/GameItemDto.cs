using LifeManager.Domain.GameItems;
using LifeManager.Domain.GameItems.Enums;
using LifeManager.Domain.Habits;

namespace LifeManager.Application.GameItems.DTOs
{
    /// <summary>One shop item and where the player stands with it.</summary>
    /// <param name="HpRestore">HP the item heals at most; null for items that don't heal.</param>
    /// <param name="PurchasedInPeriod">Purchases not undone in the current <paramref name="LimitPeriod"/>.</param>
    /// <param name="Availability">A hint for the shop; the purchase checks everything again under the lock.</param>
    public record GameItemDto(
        GameItemKind Kind,
        int Price,
        int? HpRestore,
        int Limit,
        GameItemLimitPeriod LimitPeriod,
        int PurchasedInPeriod,
        GameItemAvailability Availability)
    {
        public static GameItemDto From(GameItemKind kind, PlayerProfile profile, int purchasedInPeriod)
            => new(
                kind,
                GameItemCatalog.Price(kind),
                GameItemCatalog.HpRestore(kind),
                GameItemCatalog.Limit(kind),
                GameItemCatalog.LimitPeriod(kind),
                purchasedInPeriod,
                GameItemCatalog.Availability(kind, profile, purchasedInPeriod));
    }
}
