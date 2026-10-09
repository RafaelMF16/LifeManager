using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Test.Habits
{
    public class HabitStatsTests
    {
        // Wednesday; its week runs from Monday 2026-10-05. The latest closed week is 2026-09-28..10-04.
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        private static Habit CreateHabit(
            DateOnly startDate,
            HabitKind kind = HabitKind.Positive,
            HabitFrequencyType frequencyType = HabitFrequencyType.Daily,
            HabitWeekDays weekDays = HabitWeekDays.None,
            int? timesPerWeek = null,
            bool archived = false)
            => Habit.FromPersistence(
                1, 1, "Read", null, null, kind, HabitDifficulty.Easy, frequencyType, weekDays, timesPerWeek,
                startDate, CreatedAt, archived ? CreatedAt : null, 0, 0, Today.AddDays(-2));

        private static HabitCheckIn CheckIn(DateOnly date, HabitCheckInStatus status)
            => HabitCheckIn.FromPersistence(date.DayNumber, 1, 1, date, status, CreatedAt, 0, 0, 0);

        private static HabitDayState StateOn(IReadOnlyList<HabitDay> days, DateOnly date)
            => days.Single(day => day.Date == date).State;

        [Fact]
        public void Days_ShouldListTheLastDaysOldestFirst_EndingToday()
        {
            var days = HabitStats.Days(CreateHabit(Today.AddDays(-200)), [], Today);

            Assert.Equal(HabitStats.HeatmapDays, days.Count);
            Assert.Equal(Today.AddDays(-(HabitStats.HeatmapDays - 1)), days[0].Date);
            Assert.Equal(Today, days[^1].Date);
        }

        [Fact]
        public void Days_ShouldReadEachDayFromItsRecord()
        {
            var habit = CreateHabit(Today.AddDays(-10));
            HabitCheckIn[] checkIns =
            [
                CheckIn(Today.AddDays(-6), HabitCheckInStatus.Done),
                CheckIn(Today.AddDays(-5), HabitCheckInStatus.Missed),
                CheckIn(Today.AddDays(-4), HabitCheckInStatus.Frozen),
                CheckIn(Today, HabitCheckInStatus.Done)
            ];

            var days = HabitStats.Days(habit, checkIns, Today, days: 14);

            Assert.Equal(HabitDayState.BeforeStart, StateOn(days, Today.AddDays(-11)));
            Assert.Equal(HabitDayState.Done, StateOn(days, Today.AddDays(-6)));
            Assert.Equal(HabitDayState.Missed, StateOn(days, Today.AddDays(-5)));
            Assert.Equal(HabitDayState.Frozen, StateOn(days, Today.AddDays(-4)));
            // Closed without a record (the day close hasn't judged it): nothing is inferred.
            Assert.Equal(HabitDayState.None, StateOn(days, Today.AddDays(-3)));
            Assert.Equal(HabitDayState.Pending, StateOn(days, Today.AddDays(-1)));
            Assert.Equal(HabitDayState.Done, StateOn(days, Today));
        }

        [Fact]
        public void Days_ShouldMarkASetDaysHabitsDaysOff()
        {
            var habit = CreateHabit(Today.AddDays(-30), frequencyType: HabitFrequencyType.WeekDays,
                weekDays: HabitWeekDays.Monday | HabitWeekDays.Wednesday);

            var days = HabitStats.Days(habit, [], Today, days: 3);

            Assert.Equal(HabitDayState.Pending, StateOn(days, Today));
            Assert.Equal(HabitDayState.Off, StateOn(days, Today.AddDays(-1)));
            Assert.Equal(HabitDayState.None, StateOn(days, Today.AddDays(-2)));
        }

        [Fact]
        public void Days_ShouldShowCleanAndRelapseDays_OfAHabitToAvoid()
        {
            var habit = CreateHabit(Today.AddDays(-30), kind: HabitKind.Negative);

            var days = HabitStats.Days(habit, [CheckIn(Today.AddDays(-3), HabitCheckInStatus.Clean), CheckIn(Today, HabitCheckInStatus.Relapse)], Today, days: 5);

            Assert.Equal(HabitDayState.Clean, StateOn(days, Today.AddDays(-3)));
            Assert.Equal(HabitDayState.Relapse, StateOn(days, Today));
            Assert.Equal(HabitDayState.Pending, StateOn(days, Today.AddDays(-1)));
        }

        [Fact]
        public void Days_ShouldNeverBePending_ForAWeeklyOrArchivedHabit()
        {
            var weekly = CreateHabit(Today.AddDays(-30), frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 3);
            var archived = CreateHabit(Today.AddDays(-30), archived: true);

            Assert.Equal(HabitDayState.None, StateOn(HabitStats.Days(weekly, [], Today, days: 2), Today));
            Assert.Equal(HabitDayState.None, StateOn(HabitStats.Days(archived, [], Today, days: 2), Today));
        }

        [Fact]
        public void Consistency_ShouldLeaveFrozenDaysOut_ForADailyHabit()
        {
            var habit = CreateHabit(Today.AddDays(-60));
            HabitCheckIn[] checkIns =
            [
                CheckIn(Today.AddDays(-2), HabitCheckInStatus.Done),
                CheckIn(Today.AddDays(-3), HabitCheckInStatus.Done),
                CheckIn(Today.AddDays(-4), HabitCheckInStatus.Missed),
                CheckIn(Today.AddDays(-5), HabitCheckInStatus.Frozen),
                CheckIn(Today, HabitCheckInStatus.Done),
                // Outside the 30 days.
                CheckIn(Today.AddDays(-HabitStats.ConsistencyDays), HabitCheckInStatus.Missed)
            ];

            var consistency = HabitStats.Consistency(habit, checkIns, Today);

            Assert.Equal(new HabitConsistency(3, 4, 75, HabitConsistencyUnit.Days), consistency);
        }

        [Fact]
        public void Consistency_ShouldCountRelapsesAsNotKept_ForAHabitToAvoid()
        {
            var habit = CreateHabit(Today.AddDays(-60), kind: HabitKind.Negative);

            var consistency = HabitStats.Consistency(habit,
                [CheckIn(Today.AddDays(-2), HabitCheckInStatus.Clean), CheckIn(Today.AddDays(-3), HabitCheckInStatus.Clean), CheckIn(Today, HabitCheckInStatus.Relapse)],
                Today);

            Assert.Equal(new HabitConsistency(2, 3, 67, HabitConsistencyUnit.Days), consistency);
        }

        [Fact]
        public void Consistency_ShouldBeNull_WhileNothingWasJudged()
        {
            var consistency = HabitStats.Consistency(CreateHabit(Today), [], Today);

            Assert.Equal(new HabitConsistency(0, 0, null, HabitConsistencyUnit.Days), consistency);
        }

        [Fact]
        public void Consistency_ShouldIgnoreDaysBeforeTheStart_OfANewHabit()
        {
            var habit = CreateHabit(Today.AddDays(-2));

            var consistency = HabitStats.Consistency(habit,
                [CheckIn(Today.AddDays(-3), HabitCheckInStatus.Missed), CheckIn(Today.AddDays(-2), HabitCheckInStatus.Done)],
                Today);

            Assert.Equal(new HabitConsistency(1, 1, 100, HabitConsistencyUnit.Days), consistency);
        }

        [Fact]
        public void Consistency_ShouldCountClosedWeeksMeetingTheTarget_ForATimesPerWeekHabit()
        {
            var habit = CreateHabit(Today.AddDays(-200), frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 3);
            var lastWeek = new DateOnly(2026, 9, 28);
            HabitCheckIn[] checkIns =
            [
                // Met.
                CheckIn(lastWeek, HabitCheckInStatus.Done),
                CheckIn(lastWeek.AddDays(2), HabitCheckInStatus.Done),
                CheckIn(lastWeek.AddDays(4), HabitCheckInStatus.Done),
                // Met with a freeze's help.
                CheckIn(lastWeek.AddDays(-7), HabitCheckInStatus.Done),
                CheckIn(lastWeek.AddDays(-6), HabitCheckInStatus.Done),
                CheckIn(lastWeek.AddDays(-5), HabitCheckInStatus.Frozen),
                // Short.
                CheckIn(lastWeek.AddDays(-14), HabitCheckInStatus.Done),
                // The current week isn't closed: left out.
                CheckIn(Today, HabitCheckInStatus.Done)
            ];

            var consistency = HabitStats.Consistency(habit, checkIns, Today);

            Assert.Equal(new HabitConsistency(2, 4, 50, HabitConsistencyUnit.Weeks), consistency);
        }

        [Fact]
        public void Consistency_ShouldWaitForLastWeeksSundayToClose_OnAMonday()
        {
            var monday = new DateOnly(2026, 10, 5);
            var habit = CreateHabit(new DateOnly(2026, 9, 21), frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 1);

            // Sunday 10-04 is still editable on Monday: only the week of 09-21 is closed.
            var consistency = HabitStats.Consistency(habit, [CheckIn(new DateOnly(2026, 9, 22), HabitCheckInStatus.Done)], monday);

            Assert.Equal(new HabitConsistency(1, 1, 100, HabitConsistencyUnit.Weeks), consistency);
        }

        [Fact]
        public void Consistency_ShouldSkipWeeksThatStartedBeforeTheHabit()
        {
            // Started on Tuesday 09-22: its first week (from Monday 09-21) is never judged.
            var habit = CreateHabit(new DateOnly(2026, 9, 22), frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 1);

            var consistency = HabitStats.Consistency(habit, [], Today);

            Assert.Equal(new HabitConsistency(0, 1, 0, HabitConsistencyUnit.Weeks), consistency);
        }

        [Fact]
        public void Consistency_ShouldCountWeeksWithinTheLimit_ForAWeeklyLimit()
        {
            var habit = CreateHabit(Today.AddDays(-200), kind: HabitKind.Negative, frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);
            var lastWeek = new DateOnly(2026, 9, 28);
            HabitCheckIn[] checkIns =
            [
                // Over the limit.
                CheckIn(lastWeek, HabitCheckInStatus.Relapse),
                CheckIn(lastWeek.AddDays(1), HabitCheckInStatus.Relapse),
                CheckIn(lastWeek.AddDays(2), HabitCheckInStatus.Relapse),
                // At the limit.
                CheckIn(lastWeek.AddDays(-7), HabitCheckInStatus.Relapse),
                CheckIn(lastWeek.AddDays(-6), HabitCheckInStatus.Relapse)
            ];

            var consistency = HabitStats.Consistency(habit, checkIns, Today);

            Assert.Equal(new HabitConsistency(3, 4, 75, HabitConsistencyUnit.Weeks), consistency);
        }
    }
}
