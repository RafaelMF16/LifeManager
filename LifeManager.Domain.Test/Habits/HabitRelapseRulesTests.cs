using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;

namespace LifeManager.Domain.Test.Habits
{
    public class HabitRelapseRulesTests
    {
        // Wednesday; its week runs from Monday 2026-10-05.
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateOnly StartDate = new(2026, 9, 1);
        private static readonly DateTimeOffset Now = new(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);

        private static Habit Stored(
            HabitKind kind = HabitKind.Negative,
            HabitFrequencyType frequencyType = HabitFrequencyType.Daily,
            HabitWeekDays weekDays = HabitWeekDays.None,
            int? timesPerWeek = null,
            DateOnly? startDate = null)
            => Habit.FromPersistence(
                1, 1, "Video games", null, null, kind, HabitDifficulty.Medium, frequencyType, weekDays, timesPerWeek,
                startDate ?? StartDate, Now, archivedAt: null, currentStreak: 0, longestStreak: 0, evaluatedUntil: Today.AddDays(-2));

        private static HashSet<DateOnly> DaysAgo(params int[] days)
            => [.. days.Select(daysAgo => Today.AddDays(-daysAgo))];

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void EnsureCanRelapse_ShouldAcceptTodayAndYesterday(int daysAgo)
        {
            Assert.True(Stored().EnsureCanRelapse(Today.AddDays(-daysAgo), Today).IsSuccess);
        }

        [Fact]
        public void EnsureCanRelapse_ShouldRejectAHabitToBuild()
        {
            Assert.Equal(HabitErrors.NotAvoidable, Stored(kind: HabitKind.Positive).EnsureCanRelapse(Today, Today).Error);
        }

        [Fact]
        public void EnsureCanRelapse_ShouldRejectAFreeDay()
        {
            // Avoided on weekdays only; Saturday 3 is free.
            var habit = Stored(frequencyType: HabitFrequencyType.WeekDays,
                weekDays: HabitWeekDays.Monday | HabitWeekDays.Tuesday | HabitWeekDays.Wednesday | HabitWeekDays.Thursday | HabitWeekDays.Friday);

            Assert.True(habit.EnsureCanRelapse(Today, Today).IsSuccess);
            Assert.Equal(HabitErrors.NotScheduled, habit.EnsureCanRelapse(new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 4)).Error);
        }

        [Fact]
        public void EnsureCanRelapse_ShouldRejectDaysOutsideTheWindowOrBeforeTheStart()
        {
            Assert.Equal(HabitErrors.CheckInOutsideWindow, Stored().EnsureCanRelapse(Today.AddDays(-2), Today).Error);
            Assert.Equal(HabitErrors.CheckInOutsideWindow, Stored(startDate: Today).EnsureCanRelapse(Today.AddDays(-1), Today).Error);
        }

        [Fact]
        public void EnsureCanRelapse_ShouldRejectAnArchivedHabit()
        {
            var habit = Stored();
            habit.Archive(Now);

            Assert.Equal(HabitErrors.Archived, habit.EnsureCanRelapse(Today, Today).Error);
        }

        [Fact]
        public void RelapseDamage_ShouldCostTheDifficultysDamage_ForADailyHabit()
        {
            Assert.Equal(8, HabitRewards.RelapseDamage(Stored(), 1));
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(2, 0)]
        [InlineData(3, 8)]
        public void RelapseDamage_ShouldOnlyCostPastAWeeklyLimit(int weekRelapseCount, int expected)
        {
            var habit = Stored(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);

            Assert.Equal(expected, HabitRewards.RelapseDamage(habit, weekRelapseCount));
        }

        [Fact]
        public void Current_ShouldBreakTheStreak_OnARelapseToday()
        {
            // Clean for the last 5 judged days, then a relapse today.
            var streak = StreakCalculator.Current(Stored(), DaysAgo(2, 3, 4, 5, 6), DaysAgo(0), Today);

            Assert.Equal(0, streak);
        }

        [Fact]
        public void Current_ShouldCountCleanDays_UntilTheLastRelapse()
        {
            var streak = StreakCalculator.Current(Stored(), DaysAgo(2, 3, 5, 6), DaysAgo(4), Today);

            Assert.Equal(2, streak);
        }

        [Fact]
        public void Current_ShouldCountWeeksWithinAWeeklyLimit()
        {
            // Limit 2: a week counts when its 5 relapse-free days were judged clean.
            var habit = Stored(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);
            var lastWeek = Enumerable.Range(0, 5).Select(offset => new DateOnly(2026, 9, 28).AddDays(offset));

            Assert.Equal(1, StreakCalculator.Current(habit, lastWeek.ToHashSet(), new HashSet<DateOnly>(), Today));
        }

        [Fact]
        public void Current_ShouldBreak_WhenTheOpenWeekAlreadyPassedTheLimit()
        {
            var habit = Stored(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 1);
            var lastWeek = Enumerable.Range(0, 6).Select(offset => new DateOnly(2026, 9, 28).AddDays(offset));
            // Two relapses this week (Monday and Tuesday) with a limit of 1.
            var relapses = new HashSet<DateOnly> { new(2026, 10, 5), new(2026, 10, 6) };

            Assert.Equal(0, StreakCalculator.Current(habit, lastWeek.ToHashSet(), relapses, Today));
        }
    }
}
