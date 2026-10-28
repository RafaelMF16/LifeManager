using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Habits.Mocks
{
    public class PlayerProfileRepositoryMock : IPlayerProfileRepository
    {
        private readonly PlayerProfileSingleton _profiles;
        private readonly GameLedgerEntrySingleton _ledger;

        public PlayerProfileRepositoryMock()
        {
            _profiles = PlayerProfileSingleton.Instance;
            _ledger = GameLedgerEntrySingleton.Instance;
        }

        public Task<PlayerProfile?> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken)
        {
            var storedProfile = _profiles.SingleOrDefault(profile => profile.UserId == userId);

            // Returns a detached copy, like AsNoTracking in the real repository, so changes only persist through ApplyAsync
            return Task.FromResult(storedProfile is null ? null : ToDetachedCopy(storedProfile));
        }

        public Task<PlayerProfile> ApplyAsync(UserId userId, Func<PlayerProfile, IReadOnlyList<GameLedgerEntry>> apply, CancellationToken cancellationToken)
        {
            // Mirrors PlayerWallet: insert the default profile if missing, then work on the stored row.
            var index = EnsureExists(userId);

            var lockedProfile = ToDetachedCopy(_profiles[index]);
            var entries = apply(lockedProfile);

            foreach (var entry in entries)
            {
                entry.AssignId(_ledger.Count + 1);
                _ledger.Add(entry);
            }

            _profiles[index] = ToDetachedCopy(lockedProfile);

            return Task.FromResult(lockedProfile);
        }

        public Task UpdateAppearanceAsync(UserId userId, PlayerAvatar avatar, AchievementCode? title, CancellationToken cancellationToken)
        {
            var index = EnsureExists(userId);
            var stored = _profiles[index];

            _profiles[index] = PlayerProfile.FromPersistence(
                stored.Id!.Value, stored.UserId.Value, stored.TotalXp, stored.Hp, stored.MaxHp, stored.Coins, stored.StreakFreezes, avatar, title);

            return Task.CompletedTask;
        }

        private int EnsureExists(UserId userId)
        {
            var index = _profiles.FindIndex(profile => profile.UserId == userId);
            if (index >= 0)
                return index;

            var newProfile = PlayerProfile.CreateDefault(userId);
            newProfile.AssignId(_profiles.Count + 1);
            _profiles.Add(newProfile);

            return _profiles.Count - 1;
        }

        private static PlayerProfile ToDetachedCopy(PlayerProfile profile)
            => PlayerProfile.FromPersistence(
                profile.Id!.Value,
                profile.UserId.Value,
                profile.TotalXp,
                profile.Hp,
                profile.MaxHp,
                profile.Coins,
                profile.StreakFreezes,
                profile.Avatar,
                profile.Title);
    }
}
