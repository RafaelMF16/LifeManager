using LifeManager.Application.Habits.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.Habits.Mocks;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Habits
{
    [Collection("ApplicationServices")]
    public class HabitAutoCheckInServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly UserId OtherUserId = new(2);

        // A Wednesday.
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateOnly StartDate = new(2026, 9, 1);
        private static readonly DateTimeOffset CreatedAt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly HabitAutoCheckInService _service;

        public HabitAutoCheckInServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<HabitAutoCheckInService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);

            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            HabitProgressEntrySingleton.Instance.Clear();
            PlayerProfileSingleton.Instance.Clear();
            GameLedgerEntrySingleton.Instance.Clear();
            HabitCheckInRepositoryMock.FailNextRecord = null;
        }

        private static Habit SeedHabit(
            string name = "Log my spending",
            HabitAutoTrigger autoTrigger = HabitAutoTrigger.TransactionCreated,
            HabitFrequencyType frequencyType = HabitFrequencyType.Daily,
            HabitWeekDays weekDays = HabitWeekDays.None,
            int? timesPerWeek = null,
            DateOnly? startDate = null,
            UserId? userId = null,
            bool archived = false)
        {
            var habit = Habit.FromPersistence(
                HabitSingleton.Instance.Count + 1, (userId ?? UserId).Value, name, null, null, HabitKind.Positive, HabitDifficulty.Easy,
                frequencyType, weekDays, timesPerWeek, startDate ?? StartDate, CreatedAt,
                archived ? CreatedAt : null, currentStreak: 0, longestStreak: 0, evaluatedUntil: Today.AddDays(-2),
                autoTrigger: autoTrigger);
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

        private static PlayerProfile Profile() => PlayerProfileSingleton.Instance.Single(profile => profile.UserId == UserId);

        private Task<IReadOnlyList<Application.Habits.DTOs.AutoCheckInDto>> OnEvent(HabitAutoTrigger trigger = HabitAutoTrigger.TransactionCreated)
            => _service.OnEventAsync(UserId, trigger, CancellationToken.None);

        [Fact]
        public async Task OnEventAsync_ShouldCheckInTheHabitToday_AndPayItsReward()
        {
            var habit = SeedHabit();

            var checkIns = await OnEvent();

            var item = Assert.Single(checkIns);
            Assert.Equal(habit.Id!.Value, item.HabitId);
            Assert.Equal("Log my spending", item.Name);
            Assert.Equal(GameRules.Reward(HabitDifficulty.Easy).Coins, item.Wallet.CoinsDelta);
            var checkIn = Assert.Single(HabitCheckInSingleton.Instance);
            Assert.Equal((Today, HabitCheckInStatus.Done), (checkIn.Date, checkIn.Status));
            Assert.Equal(GameRules.Reward(HabitDifficulty.Easy).Coins, Profile().Coins);
        }

        [Fact]
        public async Task OnEventAsync_ShouldDoNothing_WhenTheHabitIsAlreadyCheckedInToday()
        {
            var habit = SeedHabit();
            SeedDone(habit, Today);

            var checkIns = await OnEvent();

            Assert.Empty(checkIns);
            Assert.Single(HabitCheckInSingleton.Instance);
        }

        [Fact]
        public async Task OnEventAsync_ShouldPayOnce_WhenTheEventRepeatsOnTheSameDay()
        {
            SeedHabit();

            await OnEvent();
            var second = await OnEvent();

            Assert.Empty(second);
            Assert.Equal(GameRules.Reward(HabitDifficulty.Easy).Coins, Profile().Coins);
        }

        [Fact]
        public async Task OnEventAsync_ShouldOnlyCheckInTheHabitsOfThatTrigger()
        {
            SeedHabit("Review budget", HabitAutoTrigger.BudgetPlanSaved);
            SeedHabit("Read", HabitAutoTrigger.None);

            var checkIns = await OnEvent(HabitAutoTrigger.BudgetPlanSaved);

            Assert.Equal(["Review budget"], checkIns.Select(item => item.Name));
        }

        [Fact]
        public async Task OnEventAsync_ShouldSkipAHabitNotDueToday()
        {
            SeedHabit(frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Monday);

            var checkIns = await OnEvent();

            Assert.Empty(checkIns);
            Assert.Empty(HabitCheckInSingleton.Instance);
        }

        [Fact]
        public async Task OnEventAsync_ShouldSkipAWeeklyHabit_WhoseWeekTargetIsMet()
        {
            var habit = SeedHabit(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);
            SeedDone(habit, Today.AddDays(-2), Today.AddDays(-1));

            var checkIns = await OnEvent();

            Assert.Empty(checkIns);
            Assert.Equal(2, HabitCheckInSingleton.Instance.Count);
        }

        [Fact]
        public async Task OnEventAsync_ShouldCheckInAWeeklyHabit_WhoseWeekTargetIsOpen()
        {
            var habit = SeedHabit(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 3);
            SeedDone(habit, Today.AddDays(-1));

            var checkIns = await OnEvent();

            Assert.Single(checkIns);
        }

        [Fact]
        public async Task OnEventAsync_ShouldSkipArchivedHabits_AndOtherUsersHabits()
        {
            SeedHabit("Archived", archived: true);
            SeedHabit("Someone else's", userId: OtherUserId);

            var checkIns = await OnEvent();

            Assert.Empty(checkIns);
            Assert.Empty(HabitCheckInSingleton.Instance);
        }

        [Fact]
        public async Task OnEventAsync_ShouldSkipAHabitThatStartsLater()
        {
            SeedHabit(startDate: Today.AddDays(1));

            var checkIns = await OnEvent();

            Assert.Empty(checkIns);
        }

        [Fact]
        public async Task OnEventAsync_ShouldSwallowAFailure_AndReturnWhatWasCheckedInBefore()
        {
            SeedHabit("A first");
            SeedHabit("B second");
            HabitCheckInRepositoryMock.FailNextRecord = (habit, _) => habit.Name.Value == "B second";

            var checkIns = await OnEvent();

            Assert.Equal(["A first"], checkIns.Select(item => item.Name));
        }

        [Fact]
        public async Task OnEventAsync_ShouldReturnNothing_WithoutAutomaticHabits()
        {
            var checkIns = await OnEvent();

            Assert.Empty(checkIns);
        }
    }
}
