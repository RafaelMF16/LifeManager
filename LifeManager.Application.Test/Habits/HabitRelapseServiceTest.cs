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
    public class HabitRelapseServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);

        // Wednesday; its week runs from Monday 2026-10-05.
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateOnly Yesterday = Today.AddDays(-1);
        private static readonly DateTimeOffset CreatedAt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        private static readonly HabitWeekDays Weekdays =
            HabitWeekDays.Monday | HabitWeekDays.Tuesday | HabitWeekDays.Wednesday | HabitWeekDays.Thursday | HabitWeekDays.Friday;

        private readonly HabitRelapseService _service;
        private readonly HabitCheckInService _checkInService;
        private readonly FakeTimeProvider _timeProvider;

        public HabitRelapseServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<HabitRelapseService>();
            _checkInService = ServiceProvider.GetRequiredService<HabitCheckInService>();
            _timeProvider = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();
            _timeProvider.SetToday(Today);

            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            PlayerProfileSingleton.Instance.Clear();
            GameLedgerEntrySingleton.Instance.Clear();
        }

        private static Habit SeedHabit(
            HabitKind kind = HabitKind.Negative,
            HabitDifficulty difficulty = HabitDifficulty.Medium,
            HabitFrequencyType frequencyType = HabitFrequencyType.Daily,
            HabitWeekDays weekDays = HabitWeekDays.None,
            int? timesPerWeek = null,
            int currentStreak = 0,
            string name = "Video games")
        {
            var habit = Habit.FromPersistence(
                HabitSingleton.Instance.Count + 1, UserId.Value, name, null, null, kind, difficulty, frequencyType, weekDays, timesPerWeek,
                new DateOnly(2026, 9, 1), CreatedAt, null, currentStreak, currentStreak, Today.AddDays(-2));
            HabitSingleton.Instance.Add(habit);

            return habit;
        }

        private static void SeedDay(Habit habit, DateOnly date, HabitCheckInStatus status)
            => HabitCheckInSingleton.Instance.Add(HabitCheckIn.FromPersistence(
                HabitCheckInSingleton.Instance.Count + 1, habit.Id!.Value, UserId.Value, date, status, CreatedAt, 0, 0, 0));

        private static void SeedProfile(int hp = GameRules.MaxHp, int coins = 0)
            => PlayerProfileSingleton.Instance.Add(PlayerProfile.FromPersistence(1, UserId.Value, 0, hp, GameRules.MaxHp, coins, 0));

        private static PlayerProfile Profile() => PlayerProfileSingleton.Instance.Single(profile => profile.UserId == UserId);

        private Task<Result<HabitRelapseResultDto>> Relapse(Habit habit, DateOnly? date = null)
            => _service.RelapseAsync(habit.Id!.Value, new HabitRelapseDto(date ?? Today), UserId, CancellationToken.None);

        [Fact]
        public async Task RelapseAsync_ShouldCostTheDifficultysDamageAndBreakTheStreak()
        {
            SeedProfile();
            var habit = SeedHabit(difficulty: HabitDifficulty.Medium, currentStreak: 4);
            foreach (var daysAgo in Enumerable.Range(2, 4))
                SeedDay(habit, Today.AddDays(-daysAgo), HabitCheckInStatus.Clean);

            var result = await Relapse(habit);

            Assert.True(result.IsSuccess);
            Assert.True(result.Value.Relapsed);
            Assert.Equal(-8, result.Value.Wallet.HpDelta);
            Assert.Equal(GameRules.MaxHp - 8, Profile().Hp);
            Assert.Equal(0, result.Value.CurrentStreak);
            var relapse = HabitCheckInSingleton.Instance.Single(day => day.Date == Today);
            Assert.Equal((HabitCheckInStatus.Relapse, -8), (relapse.Status, relapse.HpAwarded));
            var entry = Assert.Single(GameLedgerEntrySingleton.Instance);
            Assert.Equal((GameLedgerEntryKind.Relapse, habit.Id), (entry.Kind, entry.HabitId));
        }

        [Fact]
        public async Task RelapseAsync_ShouldCostNothing_WithinAWeeklyLimit()
        {
            SeedProfile();
            var habit = SeedHabit(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);
            SeedDay(habit, new DateOnly(2026, 10, 5), HabitCheckInStatus.Relapse);

            var result = await Relapse(habit);

            Assert.Equal(0, result.Value!.Wallet.HpDelta);
            Assert.Equal(2, result.Value.WeekRelapseCount);
            Assert.Equal(GameRules.MaxHp, Profile().Hp);
        }

        [Fact]
        public async Task RelapseAsync_ShouldCost_ARelapsePastTheWeeklyLimit()
        {
            SeedProfile();
            var habit = SeedHabit(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);
            SeedDay(habit, new DateOnly(2026, 10, 5), HabitCheckInStatus.Relapse);
            SeedDay(habit, Yesterday, HabitCheckInStatus.Relapse);

            var result = await Relapse(habit);

            Assert.Equal(-8, result.Value!.Wallet.HpDelta);
            Assert.Equal(3, result.Value.WeekRelapseCount);
        }

        [Fact]
        public async Task RelapseAsync_ShouldAcceptYesterday_AndRejectTheDayBefore()
        {
            var habit = SeedHabit();

            Assert.True((await Relapse(habit, Yesterday)).IsSuccess);
            Assert.Equal(HabitErrors.CheckInOutsideWindow, (await Relapse(habit, Today.AddDays(-2))).Error);
        }

        [Fact]
        public async Task RelapseAsync_ShouldRejectAFreeDay()
        {
            var habit = SeedHabit(frequencyType: HabitFrequencyType.WeekDays, weekDays: Weekdays);
            // A Sunday: free.
            _timeProvider.SetToday(new DateOnly(2026, 10, 11));

            var result = await _service.RelapseAsync(habit.Id!.Value, new HabitRelapseDto(new DateOnly(2026, 10, 11)), UserId, CancellationToken.None);

            Assert.Equal(HabitErrors.NotScheduled, result.Error);
        }

        [Fact]
        public async Task RelapseAsync_ShouldRejectAHabitToBuild()
        {
            Assert.Equal(HabitErrors.NotAvoidable, (await Relapse(SeedHabit(kind: HabitKind.Positive))).Error);
        }

        [Fact]
        public async Task RelapseAsync_ShouldReturnConflict_WhenTheDayAlreadyHasARelapse()
        {
            SeedProfile();
            var habit = SeedHabit();
            await Relapse(habit);

            var result = await Relapse(habit);

            Assert.Equal(HabitErrors.AlreadyRelapsed, result.Error);
            Assert.Equal(GameRules.MaxHp - 8, Profile().Hp);
        }

        [Fact]
        public async Task UndoRelapseAsync_ShouldGiveTheHpBackAndRestoreTheStreak()
        {
            SeedProfile();
            var habit = SeedHabit(currentStreak: 3);
            foreach (var daysAgo in Enumerable.Range(2, 3))
                SeedDay(habit, Today.AddDays(-daysAgo), HabitCheckInStatus.Clean);
            await Relapse(habit);

            var result = await _service.UndoRelapseAsync(habit.Id!.Value, Today, UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.False(result.Value.Relapsed);
            Assert.Equal(8, result.Value.Wallet.HpDelta);
            Assert.Equal(GameRules.MaxHp, Profile().Hp);
            Assert.Equal(3, result.Value.CurrentStreak);
            Assert.DoesNotContain(HabitCheckInSingleton.Instance, day => day.Date == Today);
            Assert.Equal(GameLedgerEntryKind.Undo, GameLedgerEntrySingleton.Instance.Last().Kind);
        }

        [Fact]
        public async Task UndoRelapseAsync_ShouldReturnNotFound_WhenThereIsNoRelapse()
        {
            var habit = SeedHabit();

            var result = await _service.UndoRelapseAsync(habit.Id!.Value, Today, UserId, CancellationToken.None);

            Assert.Equal(HabitErrors.RelapseNotFound, result.Error);
        }

        [Fact]
        public async Task GetTodayAsync_ShouldListHabitsToAvoidAndTheFreeOnes()
        {
            var daily = SeedHabit(name: "Smoking");
            SeedHabit(name: "Video games", frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Saturday | HabitWeekDays.Sunday);
            var weekly = SeedHabit(name: "Sweets", frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);
            SeedDay(daily, Yesterday, HabitCheckInStatus.Relapse);
            SeedDay(weekly, new DateOnly(2026, 10, 5), HabitCheckInStatus.Relapse);
            SeedDay(weekly, Yesterday, HabitCheckInStatus.Relapse);

            var result = (await _checkInService.GetTodayAsync(UserId, CancellationToken.None)).Value!;

            Assert.Equal(["Smoking", "Sweets"], result.Avoiding.Select(item => item.Name));
            Assert.Equal(["Video games"], result.FreeToday);

            var smoking = result.Avoiding.Single(item => item.Name == "Smoking");
            Assert.True(smoking.RelapsedYesterday);
            Assert.False(smoking.CanRelapseYesterday);
            Assert.Equal(8, smoking.DamagePreview);

            var sweets = result.Avoiding.Single(item => item.Name == "Sweets");
            Assert.Equal(2, sweets.WeekRelapseCount);
            // The next one would pass the limit of 2.
            Assert.Equal(8, sweets.DamagePreview);

            Assert.Equal(Yesterday, result.LastMissedOn);
        }

        [Fact]
        public async Task GetTodayAsync_ShouldPreviewNoDamage_WhileWithinTheWeeklyLimit()
        {
            SeedHabit(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);

            var item = Assert.Single((await _checkInService.GetTodayAsync(UserId, CancellationToken.None)).Value!.Avoiding);

            Assert.Equal(0, item.DamagePreview);
            Assert.Equal(0, item.WeekRelapseCount);
            Assert.True(item.CanRelapseYesterday);
        }
    }
}
