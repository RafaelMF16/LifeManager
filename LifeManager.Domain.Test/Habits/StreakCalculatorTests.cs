using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;

namespace LifeManager.Domain.Test.Habits
{
    public class StreakCalculatorTests
    {
        // Wednesday; its week runs from Monday 2026-10-05 to Sunday 2026-10-11.
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateOnly LongAgo = new(2026, 1, 1);

        private static readonly HabitFrequency MondayWednesdayFriday =
            HabitFrequency.Create(HabitFrequencyType.WeekDays, HabitWeekDays.Monday | HabitWeekDays.Wednesday | HabitWeekDays.Friday, null).Value!;

        private static HabitFrequency TimesPerWeek(int times)
            => HabitFrequency.Create(HabitFrequencyType.TimesPerWeek, HabitWeekDays.None, times).Value!;

        private static HashSet<DateOnly> DaysAgo(params int[] days)
            => [.. days.Select(daysAgo => Today.AddDays(-daysAgo))];

        private static int Daily(HashSet<DateOnly> successDates, DateOnly? startDate = null)
            => StreakCalculator.Current(HabitFrequency.Daily, startDate ?? LongAgo, successDates, Today);

        [Fact]
        public void Current_ShouldCountConsecutiveDaysEndingToday()
        {
            Assert.Equal(3, Daily(DaysAgo(0, 1, 2)));
        }

        [Fact]
        public void Current_ShouldKeepTheStreak_WhenTodayIsNotDoneYet()
        {
            Assert.Equal(2, Daily(DaysAgo(1, 2)));
        }

        [Fact]
        public void Current_ShouldKeepTheStreak_WhenYesterdayCanStillBeCheckedIn()
        {
            Assert.Equal(3, Daily(DaysAgo(0, 2, 3)));
        }

        [Fact]
        public void Current_ShouldBreak_WhenADayBeforeYesterdayWasMissed()
        {
            Assert.Equal(2, Daily(DaysAgo(0, 1, 3, 4)));
        }

        [Fact]
        public void Current_ShouldBeZero_WhenNothingWasDone()
        {
            Assert.Equal(0, Daily([]));
        }

        [Fact]
        public void Current_ShouldStopAtTheStartDate()
        {
            Assert.Equal(2, Daily(DaysAgo(0, 1, 2, 3), startDate: Today.AddDays(-1)));
        }

        [Fact]
        public void Current_ShouldSkipDaysTheHabitIsNotScheduledOn_WhenFrequencyIsWeekDays()
        {
            // Wed 7, Mon 5, Fri 2, Wed 30/09: Tue, Thu, Sat and Sun in between don't count or break.
            var done = DaysAgo(0, 2, 5, 7);

            var streak = StreakCalculator.Current(MondayWednesdayFriday, LongAgo, done, Today);

            Assert.Equal(4, streak);
        }

        [Fact]
        public void Current_ShouldBreak_WhenAScheduledWeekDayWasMissed()
        {
            // Monday 5 (two days ago) was missed and can't be checked in any more.
            var done = DaysAgo(0, 5, 7);

            Assert.Equal(1, StreakCalculator.Current(MondayWednesdayFriday, LongAgo, done, Today));
        }

        [Fact]
        public void Current_ShouldCountWeeksThatMetTheTarget_WhenFrequencyIsTimesPerWeek()
        {
            // Last two weeks met 2×; this week has 1 so far and is still open.
            var done = new HashSet<DateOnly>
            {
                new(2026, 9, 29), new(2026, 10, 1),
                new(2026, 9, 22), new(2026, 9, 26),
                Today
            };

            Assert.Equal(2, StreakCalculator.Current(TimesPerWeek(2), LongAgo, done, Today));
        }

        [Fact]
        public void Current_ShouldCountTheCurrentWeek_WhenItAlreadyMetTheTarget()
        {
            var done = new HashSet<DateOnly> { new(2026, 10, 5), Today, new(2026, 9, 30) };

            Assert.Equal(2, StreakCalculator.Current(TimesPerWeek(1), LongAgo, done, Today));
        }

        [Fact]
        public void Current_ShouldBreak_WhenAClosedWeekMissedTheTarget()
        {
            // The week before last had only one of two.
            var done = new HashSet<DateOnly> { new(2026, 9, 29), new(2026, 10, 1), new(2026, 9, 22) };

            Assert.Equal(1, StreakCalculator.Current(TimesPerWeek(2), LongAgo, done, Today));
        }

        [Fact]
        public void Current_ShouldNotBreakOnLastWeek_WhenItsSundayCanStillBeCheckedIn()
        {
            var monday = new DateOnly(2026, 10, 5);
            // Last week had 1 of 2; on Monday its Sunday is still editable, so it neither counts nor breaks.
            var done = new HashSet<DateOnly> { new(2026, 10, 1), new(2026, 9, 22), new(2026, 9, 23) };

            Assert.Equal(1, StreakCalculator.Current(TimesPerWeek(2), LongAgo, done, monday));
        }

        [Theory]
        [InlineData(HabitCheckInStatus.Done, true)]
        [InlineData(HabitCheckInStatus.Frozen, true)]
        [InlineData(HabitCheckInStatus.Clean, true)]
        [InlineData(HabitCheckInStatus.Missed, false)]
        [InlineData(HabitCheckInStatus.Relapse, false)]
        public void IsSuccess_ShouldCountDoneFrozenAndCleanDays(HabitCheckInStatus status, bool expected)
        {
            var habit = Habit.FromPersistence(1, 1, "Habit", null, null, HabitKind.Positive, HabitDifficulty.Easy,
                HabitFrequencyType.Daily, HabitWeekDays.None, null, LongAgo, DateTimeOffset.UnixEpoch, null, 0, 0, LongAgo);

            Assert.Equal(expected, HabitCheckIn.Judged(habit, Today, status, DateTimeOffset.UnixEpoch, GameDelta.None).IsSuccess);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(1, true)]
        [InlineData(2, false)]
        [InlineData(-1, false)]
        public void IsEditable_ShouldOnlyAcceptTodayAndYesterday(int daysAgo, bool expected)
        {
            Assert.Equal(expected, StreakCalculator.IsEditable(Today.AddDays(-daysAgo), Today));
        }

        [Fact]
        public void WeekStart_ShouldBeTheMonday()
        {
            Assert.Equal(new DateOnly(2026, 10, 5), StreakCalculator.WeekStart(Today));
            Assert.Equal(new DateOnly(2026, 10, 5), StreakCalculator.WeekStart(new DateOnly(2026, 10, 11)));
            Assert.Equal(new DateOnly(2026, 10, 5), StreakCalculator.WeekStart(new DateOnly(2026, 10, 5)));
        }
    }
}
