using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Habits.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Habits
{
    [Collection("ApplicationServices")]
    public class HabitProgressServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly UserId OtherUserId = new(2);

        // Wednesday; its week runs from Monday 2026-10-05.
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateOnly Yesterday = Today.AddDays(-1);
        private static readonly DateOnly StartDate = new(2026, 9, 1);
        private static readonly DateTimeOffset CreatedAt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly HabitProgressService _service;

        public HabitProgressServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<HabitProgressService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);

            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            HabitProgressEntrySingleton.Instance.Clear();
            PlayerProfileSingleton.Instance.Clear();
            GameLedgerEntrySingleton.Instance.Clear();
        }

        private static Habit SeedHabit(
            int? targetValue = 2000,
            HabitFrequencyType frequencyType = HabitFrequencyType.Daily,
            int? timesPerWeek = null,
            UserId? userId = null)
        {
            var habit = Habit.FromPersistence(
                HabitSingleton.Instance.Count + 1, (userId ?? UserId).Value, "Drink water", null, null, HabitKind.Positive, HabitDifficulty.Easy,
                frequencyType, HabitWeekDays.None, timesPerWeek, StartDate, CreatedAt, archivedAt: null,
                currentStreak: 0, longestStreak: 0, evaluatedUntil: Today.AddDays(-2),
                targetValue: targetValue, targetUnit: targetValue is null ? null : "ml", targetStep: targetValue is null ? null : 250);
            HabitSingleton.Instance.Add(habit);

            return habit;
        }

        private static void SeedDone(Habit habit, params DateOnly[] dates)
        {
            foreach (var date in dates)
            {
                HabitCheckInSingleton.Instance.Add(HabitCheckIn.FromPersistence(
                    HabitCheckInSingleton.Instance.Count + 1, habit.Id!.Value, habit.UserId.Value, date, HabitCheckInStatus.Done, CreatedAt, 5, 10, 1));
            }
        }

        private static void SeedEntries(Habit habit, DateOnly date, int count, int value = 10)
        {
            for (var i = 0; i < count; i++)
            {
                HabitProgressEntrySingleton.Instance.Add(HabitProgressEntry.FromPersistence(
                    HabitProgressEntrySingleton.Instance.Count + 1, habit.Id!.Value, habit.UserId.Value, date, value, CreatedAt));
            }
        }

        private static void SeedProfile(int hp = GameRules.MaxHp, int coins = 0, int totalXp = 0)
            => PlayerProfileSingleton.Instance.Add(PlayerProfile.FromPersistence(1, UserId.Value, totalXp, hp, GameRules.MaxHp, coins, 0));

        private static PlayerProfile Profile() => PlayerProfileSingleton.Instance.Single(profile => profile.UserId == UserId);

        private Task<Result<HabitProgressResultDto>> Add(Habit habit, int value, DateOnly? date = null)
            => _service.AddAsync(habit.Id!.Value, new HabitProgressDto(date ?? Today, value), habit.UserId, CancellationToken.None);

        private Task<Result<HabitProgressResultDto>> Remove(Habit habit, int entryId)
            => _service.RemoveAsync(habit.Id!.Value, entryId, habit.UserId, CancellationToken.None);

        private async Task<HabitProgressResultDto> AddTimes(Habit habit, int times, int value, DateOnly? date = null)
        {
            HabitProgressResultDto? last = null;
            for (var i = 0; i < times; i++)
            {
                var result = await Add(habit, value, date);
                Assert.True(result.IsSuccess);
                last = result.Value;
            }

            return last!;
        }

        [Fact]
        public async Task AddAsync_ShouldOnlySumTheDay_WhileBelowTheTarget()
        {
            SeedProfile(hp: 90);
            var habit = SeedHabit();

            var result = await Add(habit, 250);

            Assert.True(result.IsSuccess);
            Assert.Equal(250, result.Value.Progress);
            Assert.False(result.Value.Done);
            Assert.Null(result.Value.Wallet);
            var entry = Assert.Single(result.Value.Entries);
            Assert.Equal(250, entry.Value);
            Assert.Single(HabitProgressEntrySingleton.Instance);
            Assert.Empty(HabitCheckInSingleton.Instance);
            Assert.Equal(90, Profile().Hp);
            Assert.Empty(GameLedgerEntrySingleton.Instance);
        }

        [Fact]
        public async Task AddAsync_ShouldCompleteTheDayOnce_WhenTheTotalReachesTheTarget()
        {
            SeedProfile(hp: 90);
            var habit = SeedHabit();

            var completed = await AddTimes(habit, 8, 250);

            Assert.True(completed.Done);
            Assert.Equal(2000, completed.Progress);
            Assert.Equal(8, completed.Entries.Count);
            Assert.Equal(1, completed.CurrentStreak);
            Assert.NotNull(completed.Wallet);
            Assert.Equal(5, completed.Wallet.CoinsDelta);
            Assert.Equal(10, completed.Wallet.XpDelta);
            Assert.Equal(GameRules.HealPerCompletion, completed.Wallet.HpDelta);
            var checkIn = Assert.Single(HabitCheckInSingleton.Instance);
            Assert.Equal(HabitCheckInStatus.Done, checkIn.Status);
            Assert.Equal(new GameDelta(5, 10, 1), checkIn.Awarded);
            var ledger = Assert.Single(GameLedgerEntrySingleton.Instance);
            Assert.Equal(GameLedgerEntryKind.HabitDone, ledger.Kind);

            var beyond = await Add(habit, 250);

            Assert.True(beyond.Value!.Done);
            Assert.Equal(2250, beyond.Value.Progress);
            Assert.Null(beyond.Value.Wallet);
            Assert.Single(HabitCheckInSingleton.Instance);
            Assert.Equal(5, Profile().Coins);
        }

        [Fact]
        public async Task AddAsync_ShouldCompleteYesterday_WhenLoggedForYesterday()
        {
            var habit = SeedHabit();

            var result = await Add(habit, 2000, Yesterday);

            Assert.True(result.Value!.Done);
            Assert.Equal(Yesterday, result.Value.Date);
            Assert.Equal(Yesterday, Assert.Single(HabitCheckInSingleton.Instance).Date);
        }

        [Fact]
        public async Task AddAsync_ShouldPayTheMilestoneAndFreeze_WhenTheStreakPassesOne()
        {
            var habit = SeedHabit();
            SeedDone(habit, [.. Enumerable.Range(1, 6).Select(daysAgo => Today.AddDays(-daysAgo))]);

            var result = await Add(habit, 2000);

            Assert.Equal(7, result.Value!.CurrentStreak);
            Assert.Equal(7, result.Value.MilestoneDays);
            Assert.Equal(GameRules.MilestoneBonus(7), result.Value.MilestoneCoins);
            Assert.Equal(1, result.Value.FreezesEarned);
            Assert.Equal(1, Profile().StreakFreezes);
        }

        [Fact]
        public async Task AddAsync_ShouldCountTheDayForTheWeek_WhenAWeeklyHabitReachesItsTarget()
        {
            var habit = SeedHabit(targetValue: 5000, frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);
            SeedDone(habit, Today.AddDays(-2));

            var result = await Add(habit, 5000);

            // The week's second check-in meets its target: one streak week, worth 7 days (bonus and milestone).
            Assert.True(result.Value!.Done);
            Assert.Equal(1, result.Value.CurrentStreak);
            Assert.Equal(7, result.Value.MilestoneDays);
            Assert.True(result.Value.Wallet!.CoinsDelta > 0);
            Assert.Equal(2, HabitCheckInSingleton.Instance.Count);
        }

        [Fact]
        public async Task AddAsync_ShouldCompleteWithoutReward_WhenTheWeeksTargetWasAlreadyMet()
        {
            var habit = SeedHabit(targetValue: 5000, frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);
            SeedDone(habit, Today.AddDays(-2), Yesterday);

            var result = await Add(habit, 5000);

            Assert.True(result.Value!.Done);
            Assert.Equal(0, result.Value.Wallet!.CoinsDelta);
            Assert.Equal(0, result.Value.Wallet.XpDelta);
            Assert.Equal(3, HabitCheckInSingleton.Instance.Count);
        }

        [Fact]
        public async Task AddAsync_ShouldKeepADayDoneBeforeTheHabitHadATarget()
        {
            var habit = SeedHabit();
            SeedDone(habit, Today);

            var result = await Add(habit, 100);

            Assert.True(result.Value!.Done);
            Assert.Equal(100, result.Value.Progress);
            Assert.Null(result.Value.Wallet);
            Assert.Single(HabitCheckInSingleton.Instance);
        }

        [Fact]
        public async Task AddAsync_ShouldReturnValidationError_WhenTheDayIsFull()
        {
            var habit = SeedHabit();
            SeedEntries(habit, Today, HabitProgress.MaxEntriesPerDay);

            var result = await Add(habit, 250);

            Assert.Equal(HabitErrors.TooManyProgressEntries, result.Error);
            Assert.Equal(HabitProgress.MaxEntriesPerDay, HabitProgressEntrySingleton.Instance.Count);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(HabitProgressEntry.MaxValue + 1)]
        public async Task AddAsync_ShouldReturnValidationError_WhenValueIsOutOfRange(int value)
        {
            var habit = SeedHabit();

            Assert.Equal(HabitErrors.InvalidProgressValue, (await Add(habit, value)).Error);
            Assert.Empty(HabitProgressEntrySingleton.Instance);
        }

        [Fact]
        public async Task AddAsync_ShouldReturnValidationError_WhenHabitHasNoTarget()
        {
            var habit = SeedHabit(targetValue: null);

            Assert.Equal(HabitErrors.NotMeasurable, (await Add(habit, 250)).Error);
        }

        [Fact]
        public async Task AddAsync_ShouldReturnValidationError_WhenDayIsOutsideTheWindow()
        {
            var habit = SeedHabit();

            Assert.Equal(HabitErrors.CheckInOutsideWindow, (await Add(habit, 250, Today.AddDays(-2))).Error);
        }

        [Fact]
        public async Task AddAsync_ShouldReturnNotFound_WhenHabitBelongsToAnotherUser()
        {
            var habit = SeedHabit(userId: OtherUserId);

            var result = await _service.AddAsync(habit.Id!.Value, new HabitProgressDto(Today, 250), UserId, CancellationToken.None);

            Assert.Equal(HabitErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task RemoveAsync_ShouldOnlyLowerTheTotal_WhenTheDayStaysAtTheTarget()
        {
            var habit = SeedHabit();
            var completed = await AddTimes(habit, 9, 250);
            var coins = Profile().Coins;

            var result = await Remove(habit, completed.Entries[0].Id);

            Assert.True(result.Value!.Done);
            Assert.Equal(2000, result.Value.Progress);
            Assert.Equal(8, result.Value.Entries.Count);
            Assert.Null(result.Value.Wallet);
            Assert.Equal(coins, Profile().Coins);
            Assert.Single(HabitCheckInSingleton.Instance);
        }

        [Fact]
        public async Task RemoveAsync_ShouldGiveBackExactlyWhatTheDayEarned_WhenItFallsBelowTheTarget()
        {
            SeedProfile(hp: 90, coins: 40, totalXp: 30);
            var habit = SeedHabit();
            SeedDone(habit, [.. Enumerable.Range(1, 6).Select(daysAgo => Today.AddDays(-daysAgo))]);
            var completed = await AddTimes(habit, 8, 250);
            Assert.Equal(1, Profile().StreakFreezes);

            var result = await Remove(habit, completed.Entries[^1].Id);

            Assert.False(result.Value!.Done);
            Assert.Equal(1750, result.Value.Progress);
            Assert.Equal(6, result.Value.CurrentStreak);
            Assert.NotNull(result.Value.Wallet);
            Assert.Equal(40, Profile().Coins);
            Assert.Equal(30, Profile().TotalXp);
            Assert.Equal(90, Profile().Hp);
            Assert.Equal(0, Profile().StreakFreezes);
            Assert.DoesNotContain(HabitCheckInSingleton.Instance, checkIn => checkIn.Date == Today);
            Assert.Equal(GameLedgerEntryKind.Undo, GameLedgerEntrySingleton.Instance[^1].Kind);
        }

        [Fact]
        public async Task RemoveAsync_ShouldUndoADayDoneBeforeTheHabitHadATarget()
        {
            SeedProfile(coins: 5, totalXp: 10);
            var habit = SeedHabit();
            SeedDone(habit, Today);
            var added = await Add(habit, 100);

            var result = await Remove(habit, added.Value!.Entries[0].Id);

            Assert.False(result.Value!.Done);
            Assert.Equal(0, result.Value.Progress);
            Assert.Empty(HabitCheckInSingleton.Instance);
            Assert.Equal(0, Profile().Coins);
        }

        [Fact]
        public async Task RemoveAsync_ShouldReturnNotFound_WhenTheEntryIsAnotherHabits()
        {
            var habit = SeedHabit();
            var other = SeedHabit(userId: OtherUserId);
            SeedEntries(other, Today, 1);

            var result = await Remove(habit, HabitProgressEntrySingleton.Instance[0].Id!.Value);

            Assert.Equal(HabitErrors.ProgressEntryNotFound, result.Error);
            Assert.Single(HabitProgressEntrySingleton.Instance);
        }

        [Fact]
        public async Task RemoveAsync_ShouldReturnValidationError_WhenTheEntryIsOutsideTheWindow()
        {
            var habit = SeedHabit();
            SeedEntries(habit, Today.AddDays(-2), 1);

            var result = await Remove(habit, HabitProgressEntrySingleton.Instance[0].Id!.Value);

            Assert.Equal(HabitErrors.CheckInOutsideWindow, result.Error);
            Assert.Single(HabitProgressEntrySingleton.Instance);
        }
    }
}
