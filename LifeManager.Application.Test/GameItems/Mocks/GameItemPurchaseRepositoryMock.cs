using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.GameItems;
using LifeManager.Domain.GameItems.Enums;
using LifeManager.Domain.GameItems.Interfaces;
using LifeManager.Domain.GameItems.ValueObjects;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.GameItems.Mocks
{
    /// <summary>
    /// Mirrors GameItemPurchaseRepository's transaction over the singletons: lock (default profile if missing), count
    /// the period's purchases not undone, decide, then write the purchase, the ledger and the profile, or nothing.
    /// </summary>
    public class GameItemPurchaseRepositoryMock : IGameItemPurchaseRepository
    {
        private readonly GameItemPurchaseSingleton _purchases = GameItemPurchaseSingleton.Instance;
        private readonly PlayerProfileSingleton _profiles = PlayerProfileSingleton.Instance;
        private readonly GameLedgerEntrySingleton _ledger = GameLedgerEntrySingleton.Instance;

        public Task<GameItemPurchase?> GetByIdAsync(GameItemPurchaseId purchaseId, UserId userId, CancellationToken cancellationToken)
        {
            var stored = _purchases.SingleOrDefault(purchase => purchase.Id == purchaseId && purchase.UserId == userId);

            return Task.FromResult(stored is null ? null : ToDetachedCopy(stored));
        }

        public Task<IReadOnlyList<GameItemPurchase>> GetByDayAsync(UserId userId, DateOnly day, CancellationToken cancellationToken)
        {
            IReadOnlyList<GameItemPurchase> purchases = [.. _purchases
                .Where(purchase => purchase.UserId == userId && purchase.PurchasedOn == day)
                .OrderBy(purchase => purchase.PurchasedAt)
                .ThenBy(purchase => purchase.Id!.Value)
                .Select(ToDetachedCopy)];

            return Task.FromResult(purchases);
        }

        public Task<int> CountActiveSinceAsync(UserId userId, GameItemKind kind, DateOnly from, CancellationToken cancellationToken)
            => Task.FromResult(CountActiveSince(userId, kind, from));

        public Task<PlayerProfile?> PurchaseAsync(
            UserId userId,
            GameItemKind kind,
            DateOnly periodStart,
            Func<PlayerProfile, int, GameItemPurchaseDecision?> decide,
            CancellationToken cancellationToken)
        {
            var profileIndex = LockProfile(userId);
            var profile = Copy(_profiles[profileIndex]);

            var decision = decide(profile, CountActiveSince(userId, kind, periodStart));
            if (decision is null)
                return Task.FromResult<PlayerProfile?>(null);

            decision.Purchase.AssignId(_purchases.Count == 0 ? 1 : _purchases.Max(stored => stored.Id!.Value) + 1);
            _purchases.Add(ToDetachedCopy(decision.Purchase));
            Save(profileIndex, profile, decision.Entries);

            return Task.FromResult<PlayerProfile?>(profile);
        }

        public Task<PlayerProfile?> UndoAsync(
            GameItemPurchase purchase,
            Func<PlayerProfile, IReadOnlyList<GameLedgerEntry>?> decide,
            CancellationToken cancellationToken)
        {
            var profileIndex = LockProfile(purchase.UserId);

            var index = _purchases.FindIndex(stored => stored.Id == purchase.Id && stored.UserId == purchase.UserId && !stored.IsUndone);
            if (index < 0)
                return Task.FromResult<PlayerProfile?>(null);

            var profile = Copy(_profiles[profileIndex]);
            var entries = decide(profile);
            if (entries is null)
                return Task.FromResult<PlayerProfile?>(null);

            _purchases[index] = ToDetachedCopy(purchase);
            Save(profileIndex, profile, entries);

            return Task.FromResult<PlayerProfile?>(profile);
        }

        private int CountActiveSince(UserId userId, GameItemKind kind, DateOnly from)
            => _purchases.Count(purchase => purchase.UserId == userId
                && purchase.Kind == kind
                && purchase.PurchasedOn >= from
                && !purchase.IsUndone);

        /// <summary>Like PlayerWallet.LockAsync: creates the default profile if missing.</summary>
        private int LockProfile(UserId userId)
        {
            var index = _profiles.FindIndex(profile => profile.UserId == userId);
            if (index >= 0)
                return index;

            var newProfile = PlayerProfile.CreateDefault(userId);
            newProfile.AssignId(_profiles.Count + 1);
            _profiles.Add(newProfile);

            return _profiles.Count - 1;
        }

        private void Save(int profileIndex, PlayerProfile profile, IReadOnlyList<GameLedgerEntry> entries)
        {
            foreach (var entry in entries)
            {
                entry.AssignId(_ledger.Count + 1);
                _ledger.Add(entry);
            }

            _profiles[profileIndex] = Copy(profile);
        }

        private static PlayerProfile Copy(PlayerProfile profile)
            => PlayerProfile.FromPersistence(
                profile.Id!.Value,
                profile.UserId.Value,
                profile.TotalXp,
                profile.Hp,
                profile.MaxHp,
                profile.Coins,
                profile.StreakFreezes);

        internal static GameItemPurchase ToDetachedCopy(GameItemPurchase purchase)
            => GameItemPurchase.FromPersistence(
                purchase.Id!.Value,
                purchase.UserId.Value,
                purchase.Kind,
                purchase.CostPaid,
                purchase.HpRestored,
                purchase.PurchasedOn,
                purchase.PurchasedAt,
                purchase.UndoneAt);
    }
}
