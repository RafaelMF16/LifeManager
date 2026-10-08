using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Habits.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Habits
{
    [Collection("ApplicationServices")]
    public class HabitCheckInServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly UserId OtherUserId = new(2);

        // Wednesday; its week runs from Monday 2026-10-05.
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateOnly Yesterday = Today.AddDays(-1);
        private static readonly DateOnly StartDate = new(2026, 9, 1);
        private static readonly DateTimeOffset CreatedAt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly HabitCheckInService _service;

        public HabitCheckInServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<HabitCheckInService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);

            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            PlayerProfileSingleton.Instance.Clear();
            GameLedgerEntrySingleton.Instance.Clear();
        }

        private static Habit SeedHabit(
            string name = "Read",
            HabitDifficulty difficulty = HabitDifficulty.Easy,
            HabitKind kind = HabitKind.Positive,
            HabitFrequencyType frequencyType = HabitFrequencyType.Daily,
            HabitWeekDays weekDays = HabitWeekDays.None,
            int? timesPerWeek = null,
            DateOnly? startDate = null,
            UserId? userId = null,
            bool archived = false)
        {
            var habit = Habit.FromPersistence(
                HabitSingleton.Instance.Count + 1, (userId ?? UserId).Value, name, null, "After dinner", kind, difficulty,
                frequencyType, weekDays, timesPerWeek, startDate ?? StartDate, CreatedAt,
                archived ? CreatedAt : null, currentStreak: 0, longestStreak: 0, evaluatedUntil: Today.AddDays(-2));
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

        private static void SeedProfile(int hp = GameRules.MaxHp, int coins = 0, int totalXp = 0)
            => PlayerProfileSingleton.Instance.Add(PlayerProfile.FromPersistence(1, UserId.Value, totalXp, hp, GameRules.MaxHp, coins, 0));

        private Task<Domain.Shared.Results.Result<HabitCheckInResultDto>> CheckIn(Habit habit, DateOnly? date = null)
            => _service.CheckInAsync(habit.Id!.Value, new HabitCheckInDto(date ?? Today), habit.UserId, CancellationToken.None);

        private static PlayerProfile Profile() => PlayerProfileSingleton.Instance.Single(profile => profile.UserId == UserId);

        [Theory]
        [InlineData(HabitDifficulty.Easy, 5, 10)]
        [InlineData(HabitDifficulty.Medium, 10, 20)]
        [InlineData(HabitDifficulty.Hard, 20, 40)]
        public async Task CheckInAsync_ShouldAwardTheDifficultysCoinsAndXp(HabitDifficulty difficulty, int coins, int xp)
        {
            var habit = SeedHabit(difficulty: difficulty);

            var result = await CheckIn(habit);

            Assert.True(result.IsSuccess);
            Assert.Equal(coins, result.Value.Wallet.CoinsDelta);
            Assert.Equal(xp, result.Value.Wallet.XpDelta);
            Assert.Equal(coins, Profile().Coins);
            Assert.Equal(xp, Profile().TotalXp);
            Assert.True(result.Value.Done);
            Assert.Equal(1, result.Value.CurrentStreak);
        }

        [Fact]
        public async Task CheckInAsync_ShouldHealOneHp()
        {
            SeedProfile(hp: 50);
            var habit = SeedHabit();

            var result = await CheckIn(habit);

            Assert.Equal(GameRules.HealPerCompletion, result.Value!.Wallet.HpDelta);
            Assert.Equal(51, Profile().Hp);
        }

        [Theory]
        [InlineData(6, 6)]    // 7-day streak: +10%
        [InlineData(13, 6)]   // 14-day streak: +20%
        [InlineData(50, 8)]   // capped at +50%
        public async Task CheckInAsync_ShouldAddTheStreakBonus_CountingThisDay(int previousDays, int expectedCoins)
        {
            var habit = SeedHabit(difficulty: HabitDifficulty.Easy, startDate: Today.AddDays(-60));
            SeedDone(habit, [.. Enumerable.Range(1, previousDays).Select(daysAgo => Today.AddDays(-daysAgo))]);

            var result = await CheckIn(habit);

            Assert.Equal(expectedCoins, result.Value!.Wallet.CoinsDelta);
            Assert.Equal(previousDays + 1, result.Value.CurrentStreak);
        }

        [Fact]
        public async Task CheckInAsync_ShouldStoreTheCheckInWithWhatItAwarded_AndALedgerEntryWithTheHabit()
        {
            SeedProfile(hp: 90);
            var habit = SeedHabit(name: "Ler");

            await CheckIn(habit);

            var checkIn = Assert.Single(HabitCheckInSingleton.Instance);
            Assert.Equal(Today, checkIn.Date);
            Assert.Equal(HabitCheckInStatus.Done, checkIn.Status);
            Assert.Equal(new GameDelta(5, 10, 1), checkIn.Awarded);
            var entry = Assert.Single(GameLedgerEntrySingleton.Instance);
            Assert.Equal(GameLedgerEntryKind.HabitDone, entry.Kind);
            Assert.Equal(habit.Id, entry.HabitId);
            Assert.Equal("Ler", entry.Description);
            Assert.Equal(Today, entry.OccurredOn);
        }

        [Fact]
        public async Task CheckInAsync_ShouldUpdateTheHabitsStreakAndRecord()
        {
            var habit = SeedHabit();
            SeedDone(habit, Yesterday);

            await CheckIn(habit);

            var stored = Assert.Single(HabitSingleton.Instance);
            Assert.Equal(2, stored.CurrentStreak);
            Assert.Equal(2, stored.LongestStreak);
        }

        [Fact]
        public async Task CheckInAsync_ShouldAcceptYesterday()
        {
            var habit = SeedHabit();

            var result = await CheckIn(habit, Yesterday);

            Assert.True(result.IsSuccess);
            Assert.Equal(Yesterday, Assert.Single(HabitCheckInSingleton.Instance).Date);
        }

        [Fact]
        public async Task CheckInAsync_ShouldReturnConflictAndChangeNothing_WhenTheDayIsAlreadyCheckedIn()
        {
            var habit = SeedHabit();
            await CheckIn(habit);

            var result = await CheckIn(habit);

            Assert.Equal(HabitErrors.AlreadyCheckedIn, result.Error);
            Assert.Single(HabitCheckInSingleton.Instance);
            Assert.Equal(5, Profile().Coins);
        }

        [Fact]
        public async Task CheckInAsync_ShouldRejectADayOutsideTheWindow()
        {
            var result = await CheckIn(SeedHabit(), Today.AddDays(-2));

            Assert.Equal(HabitErrors.CheckInOutsideWindow, result.Error);
            Assert.Empty(HabitCheckInSingleton.Instance);
        }

        [Fact]
        public async Task CheckInAsync_ShouldRejectADayTheHabitIsNotScheduledOn()
        {
            var habit = SeedHabit(frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Monday);

            Assert.Equal(HabitErrors.NotScheduled, (await CheckIn(habit)).Error);
        }

        [Fact]
        public async Task CheckInAsync_ShouldRejectAHabitToAvoid()
        {
            Assert.Equal(HabitErrors.NotCheckable, (await CheckIn(SeedHabit(kind: HabitKind.Negative))).Error);
        }

        [Fact]
        public async Task CheckInAsync_ShouldRejectAnArchivedHabit()
        {
            Assert.Equal(HabitErrors.Archived, (await CheckIn(SeedHabit(archived: true))).Error);
        }

        [Fact]
        public async Task CheckInAsync_ShouldReturnNotFound_WhenTheHabitBelongsToAnotherUser()
        {
            var habit = SeedHabit(userId: OtherUserId);

            var result = await _service.CheckInAsync(habit.Id!.Value, new HabitCheckInDto(Today), UserId, CancellationToken.None);

            Assert.Equal(HabitErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task CheckInAsync_ShouldAwardNothing_WhenATimesPerWeekTargetWasAlreadyMet()
        {
            var habit = SeedHabit(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);
            SeedDone(habit, new DateOnly(2026, 10, 5), Yesterday);

            var result = await CheckIn(habit);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.Value.Wallet.CoinsDelta);
            Assert.Equal(0, result.Value.Wallet.XpDelta);
            Assert.Equal(3, HabitCheckInSingleton.Instance.Count);
            Assert.Equal(GameDelta.None, HabitCheckInSingleton.Instance.Single(checkIn => checkIn.Date == Today).Awarded);
        }

        [Fact]
        public async Task CheckInAsync_ShouldRewardAndCountTheWeek_WhenItMeetsATimesPerWeekTarget()
        {
            var habit = SeedHabit(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);
            SeedDone(habit, new DateOnly(2026, 10, 5));

            var result = await CheckIn(habit);

            // The week now counts: a 1-week streak is worth 7 days of bonus (+10% on 5 coins).
            Assert.Equal(6, result.Value!.Wallet.CoinsDelta);
            Assert.Equal(1, result.Value.CurrentStreak);
        }

        [Fact]
        public async Task UndoCheckInAsync_ShouldGiveBackExactlyWhatTheCheckInAwarded()
        {
            SeedProfile(hp: 80, coins: 100, totalXp: 50);
            var habit = SeedHabit(difficulty: HabitDifficulty.Medium);
            await CheckIn(habit);

            var result = await _service.UndoCheckInAsync(habit.Id!.Value, Today, UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.False(result.Value.Done);
            Assert.Equal(-10, result.Value.Wallet.CoinsDelta);
            Assert.Equal(-20, result.Value.Wallet.XpDelta);
            Assert.Equal(-1, result.Value.Wallet.HpDelta);
            Assert.Equal((100, 50, 80), (Profile().Coins, Profile().TotalXp, Profile().Hp));
            Assert.Empty(HabitCheckInSingleton.Instance);
            Assert.Equal(GameLedgerEntryKind.Undo, GameLedgerEntrySingleton.Instance.Last().Kind);
            Assert.Equal(habit.Id, GameLedgerEntrySingleton.Instance.Last().HabitId);
        }

        [Fact]
        public async Task UndoCheckInAsync_ShouldRecalculateTheStreak()
        {
            var habit = SeedHabit();
            SeedDone(habit, Yesterday, Today.AddDays(-2));
            await CheckIn(habit);

            var result = await _service.UndoCheckInAsync(habit.Id!.Value, Today, UserId, CancellationToken.None);

            Assert.Equal(2, result.Value!.CurrentStreak);
            Assert.Equal(3, result.Value.LongestStreak);
        }

        [Fact]
        public async Task UndoCheckInAsync_ShouldNeverKnockThePlayerOut()
        {
            SeedProfile(hp: 1, coins: 50);
            var habit = SeedHabit();
            SeedDone(habit, Today);

            var result = await _service.UndoCheckInAsync(habit.Id!.Value, Today, UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.False(result.Value.Wallet.KnockedOut);
            Assert.Equal(1, Profile().Hp);
            Assert.Equal(45, Profile().Coins);
        }

        [Fact]
        public async Task UndoCheckInAsync_ShouldReturnNotFound_WhenTheDayIsNotCheckedIn()
        {
            var habit = SeedHabit();

            var result = await _service.UndoCheckInAsync(habit.Id!.Value, Today, UserId, CancellationToken.None);

            Assert.Equal(HabitErrors.CheckInNotFound, result.Error);
            Assert.Empty(GameLedgerEntrySingleton.Instance);
        }

        [Fact]
        public async Task UndoCheckInAsync_ShouldRejectADayOutsideTheWindow()
        {
            var habit = SeedHabit();
            SeedDone(habit, Today.AddDays(-3));

            var result = await _service.UndoCheckInAsync(habit.Id!.Value, Today.AddDays(-3), UserId, CancellationToken.None);

            Assert.Equal(HabitErrors.CheckInOutsideWindow, result.Error);
            Assert.Single(HabitCheckInSingleton.Instance);
        }

        [Fact]
        public async Task GetTodayAsync_ShouldListTodaysHabitsWithWhetherTheyAreDone()
        {
            var read = SeedHabit(name: "Read");
            SeedHabit(name: "Gym", frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Wednesday);
            SeedHabit(name: "Monday only", frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Monday);
            SeedDone(read, Today);

            var result = await _service.GetTodayAsync(UserId, CancellationToken.None);

            Assert.Equal(Today, result.Value!.Date);
            Assert.Equal(["Gym", "Read"], result.Value.Today.Select(item => item.Name));
            Assert.True(result.Value.Today.Single(item => item.Name == "Read").Done);
            Assert.False(result.Value.Today.Single(item => item.Name == "Gym").Done);
        }

        [Fact]
        public async Task GetTodayAsync_ShouldLeaveOutArchivedHabitsHabitsToAvoidAndOtherUsersHabits()
        {
            SeedHabit(name: "Archived", archived: true);
            SeedHabit(name: "Smoking", kind: HabitKind.Negative);
            SeedHabit(name: "Other", userId: OtherUserId);

            var result = await _service.GetTodayAsync(UserId, CancellationToken.None);

            Assert.Empty(result.Value!.Today);
            Assert.Empty(result.Value.YesterdayPending);
        }

        [Fact]
        public async Task GetTodayAsync_ShouldListYesterdaysUncheckedHabits()
        {
            var read = SeedHabit(name: "Read");
            var run = SeedHabit(name: "Run");
            SeedHabit(name: "Tuesday", frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Tuesday);
            SeedHabit(name: "Monday", frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Monday);
            SeedHabit(name: "New", startDate: Today);
            SeedDone(read, Yesterday);

            var result = await _service.GetTodayAsync(UserId, CancellationToken.None);

            Assert.Equal(["Run", "Tuesday"], result.Value!.YesterdayPending.Select(item => item.Name));
            Assert.All(result.Value.YesterdayPending, item => Assert.False(item.Done));
            Assert.Contains(result.Value.Today, item => item.Id == run.Id!.Value);
        }

        [Fact]
        public async Task GetTodayAsync_ShouldShowTheWeeksProgress_ForTimesPerWeekHabits()
        {
            var habit = SeedHabit(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 3);
            SeedDone(habit, new DateOnly(2026, 10, 5), Today);

            var item = Assert.Single((await _service.GetTodayAsync(UserId, CancellationToken.None)).Value!.Today);

            Assert.Equal(2, item.WeekDoneCount);
            Assert.Equal(3, item.TimesPerWeek);
            Assert.True(item.Done);
        }

        [Fact]
        public async Task GetTodayAsync_ShouldLeaveYesterdayOut_WhenATimesPerWeekTargetIsAlreadyMet()
        {
            var habit = SeedHabit(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 1);
            SeedDone(habit, new DateOnly(2026, 10, 5));

            var result = await _service.GetTodayAsync(UserId, CancellationToken.None);

            Assert.Empty(result.Value!.YesterdayPending);
            Assert.Equal(0, Assert.Single(result.Value.Today).CoinsPreview);
        }

        [Fact]
        public async Task GetTodayAsync_ShouldPreviewTheCoinsWithTheStreakBonus()
        {
            var habit = Habit.FromPersistence(
                1, UserId.Value, "Read", null, null, HabitKind.Positive, HabitDifficulty.Hard, HabitFrequencyType.Daily,
                HabitWeekDays.None, null, StartDate, CreatedAt, null, currentStreak: 6, longestStreak: 6, evaluatedUntil: Today.AddDays(-2));
            HabitSingleton.Instance.Add(habit);

            var item = Assert.Single((await _service.GetTodayAsync(UserId, CancellationToken.None)).Value!.Today);

            // The 7th day: +10% on 20 coins.
            Assert.Equal(22, item.CoinsPreview);
        }
    }
}
