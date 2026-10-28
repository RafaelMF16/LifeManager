using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Habits.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Achievements;
using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.GameItems.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Rewards;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Habits
{
    [Collection("ApplicationServices")]
    public class PlayerProfileServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly DateOnly Today = new(2026, 10, 28);

        private readonly PlayerProfileService _playerProfileService;
        private readonly PlayerWalletService _playerWalletService;

        public PlayerProfileServiceTest()
        {
            _playerProfileService = ServiceProvider.GetRequiredService<PlayerProfileService>();
            _playerWalletService = ServiceProvider.GetRequiredService<PlayerWalletService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);

            PlayerProfileSingleton.Instance.Clear();
            GameLedgerEntrySingleton.Instance.Clear();
            AchievementUnlockSingleton.Instance.Clear();
            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            RewardRedemptionSingleton.Instance.Clear();
        }

        private static void SeedProfile()
            => PlayerProfileSingleton.Instance.Add(PlayerProfile.FromPersistence(1, UserId.Value, 0, GameRules.MaxHp, GameRules.MaxHp, 0, 0));

        private static void SeedUnlock(AchievementCode code)
            => AchievementUnlockSingleton.Instance.Add(AchievementUnlock.FromPersistence(
                AchievementUnlockSingleton.Instance.Count + 1, UserId.Value, code, Today, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        private static void SeedHabit(int id, int longestStreak, HabitFrequencyType frequencyType = HabitFrequencyType.Daily)
            => HabitSingleton.Instance.Add(Habit.FromPersistence(
                id, UserId.Value, $"Habit {id}", null, null, HabitKind.Positive, HabitDifficulty.Easy, frequencyType, HabitWeekDays.None,
                frequencyType == HabitFrequencyType.TimesPerWeek ? 3 : null, Today.AddDays(-100),
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), null, 0, longestStreak, Today.AddDays(-1)));

        private static void SeedCheckIn(int habitId, DateOnly date, HabitCheckInStatus status = HabitCheckInStatus.Done)
            => HabitCheckInSingleton.Instance.Add(HabitCheckIn.FromPersistence(
                HabitCheckInSingleton.Instance.Count + 1, habitId, UserId.Value, date, status, DateTimeOffset.UnixEpoch, 0, 0, 0));

        private static void SeedLedger(
            GameLedgerEntryKind kind, int coins, DateOnly on, bool knockout = false,
            HabitId? habitId = null, RewardId? rewardId = null, GameItemKind? itemKind = null)
        {
            var outcome = new GameOutcome(new GameDelta(coins, 0, 0), 0, 0, knockout, knockout ? 7 : 0, knockout ? GameRules.MaxHp : 0);
            foreach (var entry in GameLedgerEntry.FromOutcome(UserId, kind, on, DateTimeOffset.UnixEpoch, null, outcome, habitId, rewardId, itemKind))
            {
                entry.AssignId(GameLedgerEntrySingleton.Instance.Count + 1);
                GameLedgerEntrySingleton.Instance.Add(entry);
            }
        }

        private Task<Domain.Shared.Results.Result<PlayerProfileResponseDto>> UpdateAsync(PlayerAvatar avatar, AchievementCode? title)
            => _playerProfileService.UpdateAppearanceAsync(new UpdateAppearanceRequestDto(avatar, title), UserId, CancellationToken.None);

        [Fact]
        public async Task UpdateAppearanceAsync_ShouldSaveAndReturnTheAppearance_WhenTheTitleIsUnlocked()
        {
            SeedProfile();
            SeedUnlock(AchievementCode.CheckIns10);

            var result = await UpdateAsync(PlayerAvatar.Rocket, AchievementCode.CheckIns10);

            Assert.True(result.IsSuccess);
            Assert.Equal(PlayerAvatar.Rocket, result.Value!.Avatar);
            Assert.Equal(AchievementCode.CheckIns10, result.Value.Title);
            Assert.Empty(result.Value.NewAchievements);

            var profile = await _playerWalletService.GetProfileAsync(UserId, CancellationToken.None);
            Assert.Equal(PlayerAvatar.Rocket, profile.Avatar);
            Assert.Equal(AchievementCode.CheckIns10, profile.Title);
        }

        [Fact]
        public async Task UpdateAppearanceAsync_ShouldSaveNothing_WhenTheTitleIsLocked()
        {
            SeedProfile();
            SeedUnlock(AchievementCode.CheckIns10);

            var result = await UpdateAsync(PlayerAvatar.Rocket, AchievementCode.Level25);

            Assert.Equal(PlayerProfileErrors.TitleLocked, result.Error);
            var stored = PlayerProfileSingleton.Instance.Single();
            Assert.Equal(PlayerAvatar.Cat, stored.Avatar);
            Assert.Null(stored.Title);
        }

        [Fact]
        public async Task UpdateAppearanceAsync_ShouldCreateTheProfile_WhenTheUserHasNoneYet()
        {
            var result = await UpdateAsync(PlayerAvatar.Panda, null);

            Assert.True(result.IsSuccess);
            var stored = PlayerProfileSingleton.Instance.Single();
            Assert.Equal(UserId, stored.UserId);
            Assert.Equal(PlayerAvatar.Panda, stored.Avatar);
            Assert.Equal(0, stored.Coins);
            Assert.Empty(GameLedgerEntrySingleton.Instance);
        }

        [Fact]
        public async Task UpdateAppearanceAsync_ShouldKeepTheAppearance_WhenTheWalletChangesAfterwards()
        {
            SeedProfile();
            await UpdateAsync(PlayerAvatar.Dog, null);

            await _playerWalletService.ApplyAsync(UserId, GameLedgerEntryKind.HabitDone, new GameDelta(10, 20, 0), Today, "Read", CancellationToken.None);

            var stored = PlayerProfileSingleton.Instance.Single();
            Assert.Equal(PlayerAvatar.Dog, stored.Avatar);
            Assert.Equal(10, stored.Coins);
        }

        [Fact]
        public async Task GetProfileAsync_ShouldReturnTheDefaultAppearance_WhenThePlayerNeverChangedIt()
        {
            var profile = await _playerWalletService.GetProfileAsync(UserId, CancellationToken.None);

            Assert.Equal(PlayerAvatar.Cat, profile.Avatar);
            Assert.Null(profile.Title);
        }

        [Fact]
        public async Task GetStatsAsync_ShouldCountTheWholeHistory()
        {
            SeedHabit(1, longestStreak: 9);
            SeedHabit(2, longestStreak: 2, HabitFrequencyType.TimesPerWeek);
            SeedCheckIn(1, Today.AddDays(-3));
            SeedCheckIn(1, Today.AddDays(-2));
            SeedCheckIn(2, Today.AddDays(-2));
            SeedCheckIn(2, Today.AddDays(-1), HabitCheckInStatus.Clean);
            SeedCheckIn(2, Today, HabitCheckInStatus.Missed);
            SeedUnlock(AchievementCode.CheckIns10);
            RewardRedemptionSingleton.Instance.Add(RewardRedemption.FromPersistence(1, 1, UserId.Value, "Game", null, 30, Today, DateTimeOffset.UnixEpoch, null));

            SeedLedger(GameLedgerEntryKind.HabitDone, 10, Today.AddDays(-40), habitId: new HabitId(1));
            SeedLedger(GameLedgerEntryKind.HabitDone, 10, Today.AddDays(-3), habitId: new HabitId(1));
            SeedLedger(GameLedgerEntryKind.Undo, -10, Today.AddDays(-3), habitId: new HabitId(1));
            SeedLedger(GameLedgerEntryKind.AchievementUnlocked, 25, Today);
            SeedLedger(GameLedgerEntryKind.RewardRedeemed, -30, Today, rewardId: new RewardId(1));
            SeedLedger(GameLedgerEntryKind.ItemPurchased, -40, Today, itemKind: GameItemKind.HpPotion);
            SeedLedger(GameLedgerEntryKind.Undo, 40, Today, itemKind: GameItemKind.HpPotion);
            SeedLedger(GameLedgerEntryKind.HabitMissed, -5, Today, knockout: true, habitId: new HabitId(2));

            var stats = await _playerProfileService.GetStatsAsync(UserId, CancellationToken.None);

            Assert.Equal(3, stats.CheckIns);
            Assert.Equal(1, stats.CleanDays);
            Assert.Equal(14, stats.LongestStreakDays);
            Assert.Equal(2, stats.MostKeptDaysInOneHabit);
            Assert.Equal(3, stats.ActiveDays);
            Assert.Equal(2, stats.HabitsCreated);
            Assert.Equal(1, stats.Redemptions);
            Assert.Equal(35, stats.CoinsEarned);
            Assert.Equal(30, stats.CoinsSpent);
            Assert.Equal(1, stats.Knockouts);
            Assert.Equal(1, stats.AchievementsUnlocked);
            Assert.Equal(Today.AddDays(-40), stats.PlayingSince);
        }

        [Fact]
        public async Task GetStatsAsync_ShouldReturnZeros_ForANewPlayer()
        {
            var stats = await _playerProfileService.GetStatsAsync(UserId, CancellationToken.None);

            Assert.Equal(new PlayerStatsDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null), stats);
        }

        [Fact]
        public async Task GetEvolutionAsync_ShouldEndTodayAtTheProfileTotals_StartingFromWhatCameBefore()
        {
            await _playerWalletService.ApplyAsync(UserId, GameLedgerEntryKind.HabitDone, new GameDelta(10, 20, 0), Today.AddDays(-60), "Read", CancellationToken.None);
            await _playerWalletService.ApplyAsync(UserId, GameLedgerEntryKind.HabitDone, new GameDelta(5, 15, 0), Today.AddDays(-2), "Read", CancellationToken.None);
            await _playerWalletService.ApplyAsync(UserId, GameLedgerEntryKind.RewardRedeemed, new GameDelta(-8, 0, 0), Today, "Game", CancellationToken.None);

            var result = await _playerProfileService.GetEvolutionAsync(EvolutionRange.Month, UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var points = result.Value!.Points;
            Assert.Equal(EvolutionRange.Month, result.Value.Range);
            Assert.Equal(30, points.Count);
            Assert.Equal(new EvolutionPointDto(Today.AddDays(-29), 20, 10), points[0]);
            Assert.Equal(new EvolutionPointDto(Today.AddDays(-2), 35, 15), points[^3]);

            var profile = PlayerProfileSingleton.Instance.Single();
            Assert.Equal(new EvolutionPointDto(Today, profile.TotalXp, profile.Coins), points[^1]);
        }

        [Theory]
        [InlineData(EvolutionRange.Quarter, 90)]
        [InlineData(EvolutionRange.Year, 365)]
        public async Task GetEvolutionAsync_ShouldGiveOnePointPerDayOfTheRange(EvolutionRange range, int days)
        {
            var result = await _playerProfileService.GetEvolutionAsync(range, UserId, CancellationToken.None);

            Assert.Equal(days, result.Value!.Points.Count);
            Assert.Equal(Today, result.Value.Points[^1].Date);
        }

        [Fact]
        public async Task GetEvolutionAsync_ShouldRefuseARangeOutsideTheList()
        {
            var result = await _playerProfileService.GetEvolutionAsync((EvolutionRange)9, UserId, CancellationToken.None);

            Assert.Equal(PlayerProfileErrors.UnknownEvolutionRange, result.Error);
        }
    }
}
