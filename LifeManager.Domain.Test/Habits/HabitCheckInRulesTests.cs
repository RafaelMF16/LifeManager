using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;

namespace LifeManager.Domain.Test.Habits
{
    public class HabitCheckInRulesTests
    {
        // Wednesday.
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateOnly StartDate = new(2026, 9, 1);
        private static readonly DateTimeOffset Now = new(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);

        private static Habit Stored(
            HabitKind kind = HabitKind.Positive,
            HabitFrequencyType frequencyType = HabitFrequencyType.Daily,
            HabitWeekDays weekDays = HabitWeekDays.None,
            DateOnly? startDate = null,
            int longestStreak = 0)
            => Habit.FromPersistence(
                1, 1, "Read", null, null, kind, HabitDifficulty.Easy, frequencyType, weekDays, null,
                startDate ?? StartDate, Now, archivedAt: null, currentStreak: 0, longestStreak: longestStreak, evaluatedUntil: Today.AddDays(-2));

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void EnsureCanCheckIn_ShouldAcceptTodayAndYesterday(int daysAgo)
        {
            Assert.True(Stored().EnsureCanCheckIn(Today.AddDays(-daysAgo), Today).IsSuccess);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(-1)]
        public void EnsureCanCheckIn_ShouldRejectDaysOutsideTheWindow(int daysAgo)
        {
            Assert.Equal(HabitErrors.CheckInOutsideWindow, Stored().EnsureCanCheckIn(Today.AddDays(-daysAgo), Today).Error);
        }

        [Fact]
        public void EnsureCanCheckIn_ShouldRejectADayBeforeTheStartDate()
        {
            var habit = Stored(startDate: Today);

            Assert.Equal(HabitErrors.CheckInOutsideWindow, habit.EnsureCanCheckIn(Today.AddDays(-1), Today).Error);
        }

        [Fact]
        public void EnsureCanCheckIn_ShouldRejectAHabitToAvoid()
        {
            Assert.Equal(HabitErrors.NotCheckable, Stored(kind: HabitKind.Negative).EnsureCanCheckIn(Today, Today).Error);
        }

        [Fact]
        public void EnsureCanCheckIn_ShouldRejectAnArchivedHabit()
        {
            var habit = Stored();
            habit.Archive(Now);

            Assert.Equal(HabitErrors.Archived, habit.EnsureCanCheckIn(Today, Today).Error);
        }

        [Fact]
        public void EnsureCanCheckIn_ShouldRejectADayTheHabitIsNotScheduledOn()
        {
            var habit = Stored(frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Monday);

            Assert.Equal(HabitErrors.NotScheduled, habit.EnsureCanCheckIn(Today, Today).Error);
        }

        [Fact]
        public void SetStreak_ShouldRaiseTheRecordOnlyWhenBeaten()
        {
            var habit = Stored(longestStreak: 5);

            habit.SetStreak(3);
            Assert.Equal(3, habit.CurrentStreak);
            Assert.Equal(5, habit.LongestStreak);

            habit.SetStreak(6);
            Assert.Equal(6, habit.LongestStreak);

            habit.SetStreak(0);
            Assert.Equal(0, habit.CurrentStreak);
            Assert.Equal(6, habit.LongestStreak);
        }
    }
}
