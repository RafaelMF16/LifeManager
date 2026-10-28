using LifeManager.Domain.GameItems.Enums;
using LifeManager.Domain.GameItems.Errors;
using LifeManager.Domain.GameItems.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.GameItems
{
    /// <summary>
    /// One purchase of a game item. Keeps the price paid and the HP actually healed, so an undo gives back exactly
    /// that. It can be undone only on the day it was made.
    /// </summary>
    public class GameItemPurchase
    {
        public GameItemPurchaseId? Id { get; private set; }
        public UserId UserId { get; }
        public GameItemKind Kind { get; }

        /// <summary>The price paid, in coins.</summary>
        public int CostPaid { get; }

        /// <summary>HP the potion healed (less than its restore when HP was nearly full); 0 for other items.</summary>
        public int HpRestored { get; }

        /// <summary>The game day it was made, in the business time zone; the undo window and the limits' period.</summary>
        public DateOnly PurchasedOn { get; }

        /// <summary>When it was made; orders the day's purchases.</summary>
        public DateTimeOffset PurchasedAt { get; }

        public DateTimeOffset? UndoneAt { get; private set; }

        public bool IsUndone => UndoneAt is not null;

        private GameItemPurchase(
            UserId userId,
            GameItemKind kind,
            int costPaid,
            int hpRestored,
            DateOnly purchasedOn,
            DateTimeOffset purchasedAt,
            DateTimeOffset? undoneAt)
        {
            UserId = userId;
            Kind = kind;
            CostPaid = costPaid;
            HpRestored = hpRestored;
            PurchasedOn = purchasedOn;
            PurchasedAt = purchasedAt;
            UndoneAt = undoneAt;
        }

        /// <summary>A purchase already paid for (<see cref="GameItemCatalog.Purchase"/> decides it under the profile's lock).</summary>
        public static GameItemPurchase Create(UserId userId, GameItemKind kind, int costPaid, int hpRestored, DateOnly today, DateTimeOffset now)
            => new(userId, kind, costPaid, hpRestored, today, now, undoneAt: null);

        /// <summary>Rehydrates a stored purchase, so tests can seed any state.</summary>
        internal static GameItemPurchase FromPersistence(
            int id,
            int idUser,
            GameItemKind kind,
            int costPaid,
            int hpRestored,
            DateOnly purchasedOn,
            DateTimeOffset purchasedAt,
            DateTimeOffset? undoneAt)
        {
            var purchase = new GameItemPurchase(new UserId(idUser), kind, costPaid, hpRestored, purchasedOn, purchasedAt, undoneAt);
            purchase.AssignId(id);

            return purchase;
        }

        public bool CanUndo(DateOnly today)
            => !IsUndone && PurchasedOn == today;

        /// <summary>Whether it can be undone on <paramref name="today"/>: not undone yet, and made today.</summary>
        public Result EnsureCanUndo(DateOnly today)
        {
            if (IsUndone)
                return GameItemErrors.AlreadyUndone;

            if (PurchasedOn != today)
                return GameItemErrors.UndoOutsideWindow;

            return Result.Success();
        }

        public Result Undo(DateOnly today, DateTimeOffset now)
        {
            var canUndo = EnsureCanUndo(today);
            if (!canUndo.IsSuccess)
                return canUndo;

            UndoneAt = now;
            return Result.Success();
        }

        public void AssignId(int id)
        {
            Id = new GameItemPurchaseId(id);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not GameItemPurchase other)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Id is null || other.Id is null)
                return false;

            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
            => Id?.GetHashCode() ?? base.GetHashCode();
    }
}
