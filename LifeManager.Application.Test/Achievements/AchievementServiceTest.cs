using LifeManager.Application.Achievements.Services;
using LifeManager.Application.Rewards.Services;
using LifeManager.Application.Test.Achievements.Mocks;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Achievements;
using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Rewards;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Achievements
{
    [Collection("ApplicationServices")]
    public class AchievementServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly UserId OtherUserId = new(2);
        private static readonly DateOnly Today = new(2026, 10, 28);

        private readonly AchievementService _service;

        public AchievementServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<AchievementService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);

            AchievementUnlockSingleton.Instance.Clear();
            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            RewardRedemptionSingleton.Instance.Clear();
            PlayerProfileSingleton.Instance.Clear();
            GameLedgerEntrySingleton.Instance.Clear();
            AchievementRepositoryMock.UnlockCalls = 0;
        }

        private static void SeedProfile(int totalXp = 0, int coins = 0, UserId? userId = null)
        {
            var owner = userId ?? UserId;
            PlayerProfileSingleton.Instance.Add(PlayerProfile.FromPersistence(
                PlayerProfileSingleton.Instance.Count + 1, owner.Value, totalXp, GameRules.MaxHp, GameRules.MaxHp, coins, 0));
        }

        private static void SeedHabit(int id, int longestStreak = 0, HabitFrequencyType frequencyType = HabitFrequencyType.Daily, UserId? userId = null)
        {
            HabitSingleton.Instance.Add(Habit.FromPersistence(
                id,
                (userId ?? UserId).Value,
                $"Habit {id}",
                null,
                null,
                HabitKind.Positive,
                HabitDifficulty.Easy,
                frequencyType,
                HabitWeekDays.None,
                frequencyType == HabitFrequencyType.TimesPerWeek ? 3 : null,
                Today.AddDays(-600),
                new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                null,
                0,
                longestStreak,
                Today.AddDays(-2)));
        }

        /// <summary>Check-ins of <paramref name="habitId"/> on <paramref name="count"/> days in a row, ending on <paramref name="lastDay"/>.</summary>
        private static void SeedCheckIns(int habitId, int count, HabitCheckInStatus status = HabitCheckInStatus.Done, DateOnly? lastDay = null, UserId? userId = null)
        {
            var last = lastDay ?? Today.AddDays(-1);
            for (var offset = 0; offset < count; offset++)
            {
                HabitCheckInSingleton.Instance.Add(HabitCheckIn.FromPersistence(
                    HabitCheckInSingleton.Instance.Count + 1,
                    habitId,
                    (userId ?? UserId).Value,
                    last.AddDays(-offset),
                    status,
                    DateTimeOffset.UnixEpoch,
                    0,
                    0,
                    0));
            }
        }

        private static void SeedKnockout(DateOnly on)
        {
            var outcome = new GameOutcome(new GameDelta(0, 0, -10), 0, 0, true, 0, GameRules.MaxHp);
            var entry = GameLedgerEntry.FromOutcome(UserId, GameLedgerEntryKind.HabitMissed, on, DateTimeOffset.UnixEpoch, null, outcome)
                .Single(line => line.Kind == GameLedgerEntryKind.Knockout);
            entry.AssignId(GameLedgerEntrySingleton.Instance.Count + 1);
            GameLedgerEntrySingleton.Instance.Add(entry);
        }

        private static void SeedRedemption(bool undone = false)
        {
            var at = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
            RewardRedemptionSingleton.Instance.Add(RewardRedemption.FromPersistence(
                RewardRedemptionSingleton.Instance.Count + 1, 1, UserId.Value, "Game", null, 10, new DateOnly(2026, 10, 1), at, undone ? at.AddMinutes(1) : null));
        }

        private static PlayerProfile Profile() => PlayerProfileSingleton.Instance.Single(profile => profile.UserId == UserId);

        private static IEnumerable<GameLedgerEntry> Ledger(GameLedgerEntryKind kind)
            => GameLedgerEntrySingleton.Instance.Where(entry => entry.UserId == UserId && entry.Kind == kind);

        private Task<IReadOnlyList<AchievementUnlock>> Sync(UserId? userId = null) => _service.SyncAsync(userId ?? UserId, CancellationToken.None);

        [Fact]
        public async Task SyncAsync_ShouldPayTheBonusAndRecordIt_WhenAnAchievementIsReached()
        {
            SeedProfile(coins: 5);
            SeedHabit(1);
            SeedCheckIns(1, 10);

            var unlocks = await Sync();

            Assert.Equal([AchievementCode.CheckIns10], unlocks.Select(unlock => unlock.Code));
            Assert.Equal(30, Profile().Coins);
            Assert.Equal(50, Profile().TotalXp);

            var entry = Assert.Single(Ledger(GameLedgerEntryKind.AchievementUnlocked));
            Assert.Equal(AchievementCode.CheckIns10, entry.AchievementCode);
            Assert.Equal(25, entry.CoinsDelta);
            Assert.Equal(50, entry.XpDelta);
            Assert.Equal(Today, entry.OccurredOn);
            Assert.Null(entry.Description);

            var stored = Assert.Single(AchievementUnlockSingleton.Instance);
            Assert.Equal(Today, stored.UnlockedOn);
            Assert.False(stored.IsSeen);
        }

        [Fact]
        public async Task SyncAsync_ShouldUnlockTheWholeHistoryAtOnce()
        {
            SeedProfile(totalXp: GameRules.TotalXpForLevel(3));
            SeedHabit(1, longestStreak: 8);
            SeedHabit(2);
            SeedCheckIns(1, 60);
            SeedCheckIns(2, 60);

            var unlocks = await Sync();

            Assert.Equal([AchievementCode.CheckIns10, AchievementCode.CheckIns100, AchievementCode.Streak7], unlocks.Select(unlock => unlock.Code));
            Assert.Equal(150, Profile().Coins);
            Assert.Equal(GameRules.TotalXpForLevel(3) + 250, Profile().TotalXp);
            Assert.Equal(3, Ledger(GameLedgerEntryKind.AchievementUnlocked).Count());
        }

        [Fact]
        public async Task SyncAsync_ShouldUnlockALevelAchievement_ReachedByAnotherOnesBonus()
        {
            SeedProfile(totalXp: GameRules.TotalXpForLevel(5) - 40);
            SeedHabit(1);
            SeedCheckIns(1, 10);

            var unlocks = await Sync();

            Assert.Equal([AchievementCode.CheckIns10, AchievementCode.Level5], unlocks.Select(unlock => unlock.Code));
            Assert.Equal(5, Profile().Level);
            Assert.Equal(50, Profile().Coins);

            var levelUp = Assert.Single(Ledger(GameLedgerEntryKind.LevelUp));
            Assert.Equal(AchievementCode.CheckIns10, levelUp.AchievementCode);
        }

        [Fact]
        public async Task SyncAsync_ShouldPayOnlyOnce()
        {
            SeedProfile();
            SeedHabit(1);
            SeedCheckIns(1, 10);

            await Sync();
            var second = await Sync();

            Assert.Empty(second);
            Assert.Equal(25, Profile().Coins);
            Assert.Single(AchievementUnlockSingleton.Instance);
        }

        [Fact]
        public async Task SyncAsync_ShouldNotTakeTheLock_WhenNothingIsNew()
        {
            SeedProfile();
            SeedHabit(1);
            SeedCheckIns(1, 9);

            var unlocks = await Sync();

            Assert.Empty(unlocks);
            Assert.Equal(0, AchievementRepositoryMock.UnlockCalls);
            Assert.Empty(GameLedgerEntrySingleton.Instance);
        }

        [Fact]
        public async Task SyncAsync_ShouldKeepTheAchievement_WhenTheCheckInBehindItIsUndone()
        {
            SeedProfile();
            SeedHabit(1);
            SeedCheckIns(1, 10);
            await Sync();

            HabitCheckInSingleton.Instance.RemoveAt(0);
            await Sync();

            Assert.Equal(AchievementCode.CheckIns10, Assert.Single(AchievementUnlockSingleton.Instance).Code);
            Assert.Equal(25, Profile().Coins);
        }

        [Fact]
        public async Task SyncAsync_ShouldCountAWeeklyHabitsWeeksAsSevenDays()
        {
            SeedProfile();
            SeedHabit(1, longestStreak: 1, frequencyType: HabitFrequencyType.TimesPerWeek);

            var unlocks = await Sync();

            Assert.Equal([AchievementCode.Streak7], unlocks.Select(unlock => unlock.Code));
        }

        [Fact]
        public async Task SyncAsync_ShouldCountCleanDaysApartFromCheckIns()
        {
            SeedProfile();
            SeedHabit(1);
            SeedCheckIns(1, 10, HabitCheckInStatus.Clean);

            var unlocks = await Sync();

            Assert.Equal([AchievementCode.CleanDays10], unlocks.Select(unlock => unlock.Code));
        }

        [Fact]
        public async Task SyncAsync_ShouldCountOnlyTheDaysAfterTheFirstKnockout_ForTheComeback()
        {
            SeedProfile();
            SeedHabit(1);
            SeedHabit(2);
            var knockoutDay = Today.AddDays(-20);
            SeedKnockout(knockoutDay);
            SeedCheckIns(1, 6, lastDay: knockoutDay);
            SeedCheckIns(2, 9, HabitCheckInStatus.Clean, lastDay: knockoutDay.AddDays(9));

            Assert.Empty(await Sync());

            SeedCheckIns(1, 1, lastDay: Today.AddDays(-1));
            var unlocks = await Sync();

            Assert.Equal([AchievementCode.Comeback], unlocks.Select(unlock => unlock.Code));
        }

        [Fact]
        public async Task SyncAsync_ShouldUnlockTheAutomaticHabit_OnlyFromOneHabitsDays()
        {
            SeedProfile();
            SeedHabit(1);
            SeedHabit(2);
            SeedCheckIns(1, 65);
            SeedCheckIns(2, 65, HabitCheckInStatus.Clean);

            Assert.DoesNotContain(AchievementCode.AutomaticHabit, (await Sync()).Select(unlock => unlock.Code));

            SeedCheckIns(2, 1, HabitCheckInStatus.Clean, lastDay: Today.AddDays(-100));

            Assert.Contains(AchievementCode.AutomaticHabit, (await Sync()).Select(unlock => unlock.Code));
        }

        [Fact]
        public async Task SyncAsync_ShouldIgnoreUndoneRedemptions()
        {
            SeedProfile();
            SeedRedemption(undone: true);

            Assert.Empty(await Sync());

            SeedRedemption();

            Assert.Equal([AchievementCode.Redemptions1], (await Sync()).Select(unlock => unlock.Code));
        }

        [Fact]
        public async Task SyncAsync_ShouldOnlyReadTheUsersOwnHistory()
        {
            SeedProfile();
            SeedProfile(userId: OtherUserId);
            SeedHabit(1, userId: OtherUserId);
            SeedCheckIns(1, 10, userId: OtherUserId);

            Assert.Empty(await Sync());
            Assert.Single(await Sync(OtherUserId));
        }

        [Fact]
        public async Task GetAsync_ShouldListTheCatalogWithProgressAndUnlockDates()
        {
            SeedProfile();
            SeedHabit(1);
            SeedCheckIns(1, 37);

            var result = await _service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal(16, result.Total);
            Assert.Equal(1, result.UnlockedCount);
            Assert.Equal(AchievementCatalog.All, result.Achievements.Select(achievement => achievement.Code));

            var bronze = result.Achievements.Single(achievement => achievement.Code == AchievementCode.CheckIns10);
            Assert.Equal(10, bronze.Progress);
            Assert.NotNull(bronze.UnlockedAt);

            var silver = result.Achievements.Single(achievement => achievement.Code == AchievementCode.CheckIns100);
            Assert.Equal(AchievementFamily.CheckIns, silver.Family);
            Assert.Equal(AchievementTier.Silver, silver.Tier);
            Assert.Equal(37, silver.Progress);
            Assert.Equal(100, silver.Threshold);
            Assert.Equal(100, silver.CoinsBonus);
            Assert.Equal(150, silver.XpBonus);
            Assert.Null(silver.UnlockedAt);
        }

        [Fact]
        public async Task GetAsync_ShouldShowAnUnlockedAchievementAsComplete_AfterItsNumberDropped()
        {
            SeedProfile();
            SeedHabit(1);
            SeedCheckIns(1, 10);
            await Sync();
            HabitCheckInSingleton.Instance.RemoveAt(0);

            var result = await _service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal(10, result.Achievements.Single(achievement => achievement.Code == AchievementCode.CheckIns10).Progress);
        }

        [Fact]
        public async Task EarningPace_ShouldIgnoreAchievementBonuses()
        {
            SeedProfile();
            SeedHabit(1);
            SeedCheckIns(1, 120);
            var rewardRedemptionService = ServiceProvider.GetRequiredService<RewardRedemptionService>();
            var before = await rewardRedemptionService.GetEarningPaceAsync(UserId, CancellationToken.None);

            await Sync();
            var after = await rewardRedemptionService.GetEarningPaceAsync(UserId, CancellationToken.None);

            Assert.Equal(before, after);
        }
    }
}
