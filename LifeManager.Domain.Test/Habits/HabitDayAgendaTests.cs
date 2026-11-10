using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Test.Habits
{
    public class HabitDayAgendaTests
    {
        // Wednesday.
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateOnly Yesterday = Today.AddDays(-1);
        private static readonly DateOnly StartDate = new(2026, 9, 1);
        private static readonly DateTimeOffset Now = new(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);

        private static Habit Stored(
            int id,
            HabitKind kind = HabitKind.Positive,
            HabitFrequencyType frequencyType = HabitFrequencyType.Daily,
            HabitWeekDays weekDays = HabitWeekDays.None,
            int? timesPerWeek = null,
            DateOnly? startDate = null)
            => Habit.FromPersistence(
                id, 1, $"Habit {id}", null, null, kind, HabitDifficulty.Easy, frequencyType, weekDays, timesPerWeek,
                startDate ?? StartDate, Now, archivedAt: null, currentStreak: 0, longestStreak: 0, evaluatedUntil: Today.AddDays(-2));

        private static HabitCheckIn CheckIn(int habitId, DateOnly date, HabitCheckInStatus status = HabitCheckInStatus.Done)
            => HabitCheckIn.FromPersistence(habitId * 100 + date.Day, habitId, 1, date, status, Now, 0, 0, 0);

        [Fact]
        public void For_ShouldListADailyHabitAsDueTodayAndPendingYesterday_WhenNothingWasDone()
        {
            var agenda = HabitDayAgenda.For([Stored(1)], [], [], Today);

            var due = Assert.Single(agenda.Today);
            Assert.False(due.Done);
            Assert.Equal(Today, due.Date);
            Assert.Equal(Yesterday, Assert.Single(agenda.YesterdayPending).Date);
        }

        [Fact]
        public void For_ShouldMarkTodayDoneAndDropYesterday_WhenBothWereCheckedIn()
        {
            var agenda = HabitDayAgenda.For([Stored(1)], [], [CheckIn(1, Today), CheckIn(1, Yesterday)], Today);

            Assert.True(Assert.Single(agenda.Today).Done);
            Assert.Empty(agenda.YesterdayPending);
        }

        [Fact]
        public void For_ShouldLeaveOutAHabitOnItsDayOff()
        {
            // Monday only: today (Wednesday) and yesterday (Tuesday) are days off.
            var habit = Stored(1, frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Monday);

            var agenda = HabitDayAgenda.For([habit], [], [], Today);

            Assert.Empty(agenda.Today);
            Assert.Empty(agenda.YesterdayPending);
        }

        [Fact]
        public void For_ShouldNotLeaveYesterdayPending_WhenAWeeklyHabitAlreadyMetItsWeek()
        {
            var habit = Stored(1, frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 1);

            // Monday's check-in meets the week's target.
            var agenda = HabitDayAgenda.For([habit], [], [CheckIn(1, Today.AddDays(-2))], Today);

            Assert.Single(agenda.Today);
            Assert.Empty(agenda.YesterdayPending);
        }

        [Fact]
        public void For_ShouldLeaveOutDaysBeforeTheStartDate()
        {
            var agenda = HabitDayAgenda.For([Stored(1, startDate: Today)], [], [], Today);

            Assert.Single(agenda.Today);
            Assert.Empty(agenda.YesterdayPending);
        }

        [Fact]
        public void For_ShouldReadTheRelapsesOfAHabitToAvoid()
        {
            var agenda = HabitDayAgenda.For([], [Stored(1, kind: HabitKind.Negative)], [CheckIn(1, Today, HabitCheckInStatus.Relapse)], Today);

            var avoided = Assert.Single(agenda.Avoiding);
            Assert.True(avoided.RelapsedToday);
            Assert.Contains(Today, avoided.RelapseDates);
        }

        [Fact]
        public void For_ShouldListAHabitToAvoidOnItsDayOffAsFree()
        {
            var habit = Stored(1, kind: HabitKind.Negative, frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Monday);

            var agenda = HabitDayAgenda.For([], [habit], [], Today);

            Assert.Empty(agenda.Avoiding);
            Assert.Same(habit, Assert.Single(agenda.FreeToday));
        }

        [Fact]
        public void For_ShouldIgnoreTheCheckInsOfOtherHabits()
        {
            var agenda = HabitDayAgenda.For([Stored(1)], [], [CheckIn(2, Today)], Today);

            Assert.False(Assert.Single(agenda.Today).Done);
        }
    }
}
