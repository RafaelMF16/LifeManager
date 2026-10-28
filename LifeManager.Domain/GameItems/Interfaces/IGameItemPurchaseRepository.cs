using LifeManager.Domain.GameItems.Enums;
using LifeManager.Domain.GameItems.ValueObjects;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.GameItems.Interfaces
{
    /// <summary>What a purchase stores: the purchase itself and its ledger entries.</summary>
    public record GameItemPurchaseDecision(GameItemPurchase Purchase, IReadOnlyList<GameLedgerEntry> Entries);

    /// <remarks>
    /// Buying and undoing lock the user's profile first (the same lock as every other change to the game), and the
    /// period's purchases are counted under it, so two purchases at once can't spend the same coins or pass a limit.
    /// </remarks>
    public interface IGameItemPurchaseRepository
    {
        Task<GameItemPurchase?> GetByIdAsync(GameItemPurchaseId purchaseId, UserId userId, CancellationToken cancellationToken);

        /// <summary>The user's purchases made on <paramref name="day"/>, undone ones included, oldest first.</summary>
        Task<IReadOnlyList<GameItemPurchase>> GetByDayAsync(UserId userId, DateOnly day, CancellationToken cancellationToken);

        /// <summary>The user's purchases of <paramref name="kind"/> not undone, made on or after <paramref name="from"/>.</summary>
        Task<int> CountActiveSinceAsync(UserId userId, GameItemKind kind, DateOnly from, CancellationToken cancellationToken);

        /// <summary>
        /// In one database transaction: locks the profile, counts the purchases of <paramref name="kind"/> not undone
        /// since <paramref name="periodStart"/> and calls <paramref name="decide"/> with both. It pays for the item and
        /// returns what to store, or null to refuse it. Then stores the purchase, the entries and the profile.
        /// </summary>
        /// <returns>The profile after paying; null, with nothing written, when <paramref name="decide"/> refused.</returns>
        Task<PlayerProfile?> PurchaseAsync(
            UserId userId,
            GameItemKind kind,
            DateOnly periodStart,
            Func<PlayerProfile, int, GameItemPurchaseDecision?> decide,
            CancellationToken cancellationToken);

        /// <summary>
        /// The same transaction for an undo: locks the profile, stores the <paramref name="purchase"/>'s
        /// <see cref="GameItemPurchase.UndoneAt"/> if it wasn't undone meanwhile, and calls <paramref name="decide"/>,
        /// which takes the item back and returns the entries, or null to refuse (the freeze was already used).
        /// </summary>
        /// <returns>The profile after the refund; null, with nothing written, when it was already undone or refused.</returns>
        Task<PlayerProfile?> UndoAsync(
            GameItemPurchase purchase,
            Func<PlayerProfile, IReadOnlyList<GameLedgerEntry>?> decide,
            CancellationToken cancellationToken);
    }
}
