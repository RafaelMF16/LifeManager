using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Achievements;
using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Achievements.Interfaces;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Achievements.Mocks
{
    /// <summary>
    /// Mirrors AchievementRepository over the singletons: the progress is the same counts as its SELECT, and unlocking
    /// locks (default profile if missing), reads again, decides, then writes the unlocks, the ledger and the profile, or
    /// nothing. <see cref="UnlockCalls"/> tells whether a sync took the lock; <see cref="BeforeLockedRead"/> plays a race.
    /// </summary>
    public class AchievementRepositoryMock : IAchievementRepository
    {
        private readonly AchievementUnlockSingleton _unlocks = AchievementUnlockSingleton.Instance;
        private readonly PlayerProfileSingleton _profiles = PlayerProfileSingleton.Instance;
        private readonly GameLedgerEntrySingleton _ledger = GameLedgerEntrySingleton.Instance;

        public static int UnlockCalls { get; set; }

        /// <summary>Runs after the lock and before the reads under it: lets a test play another sync that won the race.</summary>
        public static Action? BeforeLockedRead { get; set; }

        public Task<AchievementProgress> GetProgressAsync(UserId userId, CancellationToken cancellationToken)
            => Task.FromResult(Progress(userId));

        public Task<IReadOnlyList<AchievementUnlock>> GetUnlocksAsync(UserId userId, CancellationToken cancellationToken)
            => Task.FromResult(Unlocks(userId));

        public Task<IReadOnlyList<AchievementUnlock>> UnlockAsync(
            UserId userId,
            Func<PlayerProfile, AchievementProgress, IReadOnlyCollection<AchievementUnlock>, AchievementUnlockDecision?> decide,
            CancellationToken cancellationToken)
        {
            UnlockCalls++;
            BeforeLockedRead?.Invoke();
            var profileIndex = LockProfile(userId);
            var profile = Copy(_profiles[profileIndex]);

            var decision = decide(profile, Progress(userId), Unlocks(userId));
            if (decision is null)
                return Task.FromResult<IReadOnlyList<AchievementUnlock>>([]);

            foreach (var unlock in decision.Unlocks)
            {
                if (_unlocks.Any(stored => stored.UserId == unlock.UserId && stored.Code == unlock.Code))
                    throw new InvalidOperationException("Unique index (UserId, Code) violated");

                unlock.AssignId(_unlocks.Count + 1);
                _unlocks.Add(ToDetachedCopy(unlock));
            }

            foreach (var entry in decision.Entries)
            {
                entry.AssignId(_ledger.Count + 1);
                _ledger.Add(entry);
            }

            _profiles[profileIndex] = Copy(profile);

            return Task.FromResult(decision.Unlocks);
        }

        public Task MarkSeenAsync(UserId userId, IReadOnlyCollection<AchievementCode> codes, DateTimeOffset now, CancellationToken cancellationToken)
        {
            foreach (var unlock in _unlocks.Where(unlock => unlock.UserId == userId && codes.Contains(unlock.Code)))
                unlock.MarkSeen(now);

            return Task.CompletedTask;
        }

        private static AchievementProgress Progress(UserId userId)
        {
            var checkIns = HabitCheckInSingleton.Instance.Where(checkIn => checkIn.UserId == userId).ToList();
            var kept = checkIns.Where(checkIn => checkIn.Status is HabitCheckInStatus.Done or HabitCheckInStatus.Clean).ToList();
            var firstKnockoutOn = GameLedgerEntrySingleton.Instance
                .Where(entry => entry.UserId == userId && entry.Kind == GameLedgerEntryKind.Knockout)
                .Select(entry => (DateOnly?)entry.OccurredOn)
                .Min();

            return new AchievementProgress(
                checkIns.Count(checkIn => checkIn.Status == HabitCheckInStatus.Done),
                HabitSingleton.Instance
                    .Where(habit => habit.UserId == userId)
                    .Select(habit => HabitRewards.StreakBonusDays(habit, habit.LongestStreak))
                    .DefaultIfEmpty(0)
                    .Max(),
                PlayerProfileSingleton.Instance.SingleOrDefault(profile => profile.UserId == userId)?.Level ?? GameRules.StartingLevel,
                RewardRedemptionSingleton.Instance.Count(redemption => redemption.UserId == userId && redemption.UndoneAt is null),
                checkIns.Count(checkIn => checkIn.Status == HabitCheckInStatus.Clean),
                firstKnockoutOn is null ? 0 : kept.Count(checkIn => checkIn.Date > firstKnockoutOn),
                kept.GroupBy(checkIn => checkIn.HabitId).Select(days => days.Count()).DefaultIfEmpty(0).Max());
        }

        private IReadOnlyList<AchievementUnlock> Unlocks(UserId userId)
            => [.. _unlocks
                .Where(unlock => unlock.UserId == userId)
                .OrderBy(unlock => unlock.UnlockedAt)
                .ThenBy(unlock => unlock.Id!.Value)
                .Select(ToDetachedCopy)];

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

        private static PlayerProfile Copy(PlayerProfile profile)
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

        private static AchievementUnlock ToDetachedCopy(AchievementUnlock unlock)
            => AchievementUnlock.FromPersistence(unlock.Id!.Value, unlock.UserId.Value, unlock.Code, unlock.UnlockedOn, unlock.UnlockedAt, unlock.SeenAt);
    }
}
