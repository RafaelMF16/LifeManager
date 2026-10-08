using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Rewards;
using LifeManager.Domain.Rewards.Interfaces;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Rewards.Mocks
{
    /// <summary>Mirrors RewardRedemptionRepository's transaction over the singletons: profile, redemption and ledger.</summary>
    public class RewardRedemptionRepositoryMock : IRewardRedemptionRepository
    {
        private readonly RewardRedemptionSingleton _redemptions = RewardRedemptionSingleton.Instance;
        private readonly PlayerProfileSingleton _profiles = PlayerProfileSingleton.Instance;
        private readonly GameLedgerEntrySingleton _ledger = GameLedgerEntrySingleton.Instance;

        public Task<RewardRedemption?> GetByIdAsync(RewardRedemptionId redemptionId, UserId userId, CancellationToken cancellationToken)
        {
            var stored = _redemptions.SingleOrDefault(redemption => redemption.Id == redemptionId && redemption.UserId == userId);

            return Task.FromResult(stored is null ? null : ToDetachedCopy(stored));
        }

        public Task<PagedList<RewardRedemption>> GetPagedByUserIdAsync(UserId userId, PageRequest pageRequest, CancellationToken cancellationToken)
        {
            var matching = _redemptions
                .Where(redemption => redemption.UserId == userId)
                .OrderByDescending(redemption => redemption.RedeemedAt)
                .ThenByDescending(redemption => redemption.Id!.Value)
                .ToList();
            IReadOnlyList<RewardRedemption> items = [.. matching.Skip(pageRequest.Skip).Take(pageRequest.PageSize).Select(ToDetachedCopy)];

            return Task.FromResult(new PagedList<RewardRedemption>(items, matching.Count, pageRequest.Page, pageRequest.PageSize));
        }

        public Task<PlayerProfile?> RedeemAsync(
            RewardRedemption redemption,
            Func<PlayerProfile, IReadOnlyList<GameLedgerEntry>?> decide,
            CancellationToken cancellationToken)
        {
            var profileIndex = LockProfile(redemption.UserId);
            var profile = Copy(_profiles[profileIndex]);

            var entries = decide(profile);
            if (entries is null)
                return Task.FromResult<PlayerProfile?>(null);

            redemption.AssignId(_redemptions.Count == 0 ? 1 : _redemptions.Max(stored => stored.Id!.Value) + 1);
            _redemptions.Add(ToDetachedCopy(redemption));
            Save(profileIndex, profile, entries);

            return Task.FromResult<PlayerProfile?>(profile);
        }

        public Task<PlayerProfile?> UndoAsync(
            RewardRedemption redemption,
            Func<PlayerProfile, IReadOnlyList<GameLedgerEntry>> apply,
            CancellationToken cancellationToken)
        {
            var profileIndex = LockProfile(redemption.UserId);

            var index = _redemptions.FindIndex(stored => stored.Id == redemption.Id && stored.UserId == redemption.UserId && !stored.IsUndone);
            if (index < 0)
                return Task.FromResult<PlayerProfile?>(null);

            _redemptions[index] = ToDetachedCopy(redemption);

            var profile = Copy(_profiles[profileIndex]);
            Save(profileIndex, profile, apply(profile));

            return Task.FromResult<PlayerProfile?>(profile);
        }

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

        internal static RewardRedemption ToDetachedCopy(RewardRedemption redemption)
            => RewardRedemption.FromPersistence(
                redemption.Id!.Value,
                redemption.RewardId.Value,
                redemption.UserId.Value,
                redemption.RewardName,
                redemption.RewardIcon,
                redemption.CostPaid,
                redemption.RedeemedOn,
                redemption.RedeemedAt,
                redemption.UndoneAt);
    }
}
