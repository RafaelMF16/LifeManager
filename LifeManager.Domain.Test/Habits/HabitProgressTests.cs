using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.ValueObjects;

namespace LifeManager.Domain.Test.Habits
{
    public class HabitProgressTests
    {
        // Wednesday.
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateOnly StartDate = new(2026, 9, 1);
        private static readonly DateTimeOffset Now = new(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);
        private static readonly HabitTarget Target = HabitTarget.Create(2000, "ml", 250).Value!;

        private static Habit Stored(
            HabitKind kind = HabitKind.Positive,
            HabitFrequencyType frequencyType = HabitFrequencyType.Daily,
            HabitWeekDays weekDays = HabitWeekDays.None,
            int? targetValue = 2000,
            DateTimeOffset? archivedAt = null)
            => Habit.FromPersistence(
                1, 1, "Drink water", null, null, kind, HabitDifficulty.Easy, frequencyType, weekDays, null,
                StartDate, Now, archivedAt, currentStreak: 0, longestStreak: 0, evaluatedUntil: Today.AddDays(-2),
                targetValue: targetValue, targetUnit: targetValue is null ? null : "ml", targetStep: null);

        [Theory]
        [InlineData(2000, false)]
        [InlineData(2250, false)]
        public void ForAdded_ShouldComplete_WhenTheDayReachesTheTargetAndIsNotDone(int total, bool done)
        {
            Assert.Equal(HabitProgressDecision.Complete, HabitProgress.ForAdded(Target, total, done));
        }

        [Theory]
        [InlineData(1999, false)]
        [InlineData(2250, true)]
        // A day done as a one-tap habit before it got a target stays done: logging more never undoes it.
        [InlineData(250, true)]
        public void ForAdded_ShouldChangeNothing_WhenBelowTheTargetOrAlreadyDone(int total, bool done)
        {
            Assert.Equal(HabitProgressDecision.None, HabitProgress.ForAdded(Target, total, done));
        }

        [Theory]
        [InlineData(1750)]
        // Done as a one-tap habit before it got a target: the first removal undoes it.
        [InlineData(0)]
        public void ForRemoved_ShouldUndo_WhenTheDayFallsBelowTheTargetAndIsDone(int total)
        {
            Assert.Equal(HabitProgressDecision.Undo, HabitProgress.ForRemoved(Target, total, done: true));
        }

        [Theory]
        [InlineData(2000, true)]
        [InlineData(1750, false)]
        // The target was lowered: a removal never completes a day.
        [InlineData(2500, false)]
        public void ForRemoved_ShouldChangeNothing_WhenStillAtTheTargetOrNotDone(int total, bool done)
        {
            Assert.Equal(HabitProgressDecision.None, HabitProgress.ForRemoved(Target, total, done));
        }

        [Fact]
        public void EnsureCanAdd_ShouldAcceptUpToTheDailyLimit()
        {
            Assert.True(HabitProgress.EnsureCanAdd(HabitProgress.MaxEntriesPerDay - 1).IsSuccess);
        }

        [Fact]
        public void EnsureCanAdd_ShouldReject_WhenTheDayIsFull()
        {
            Assert.Equal(HabitErrors.TooManyProgressEntries, HabitProgress.EnsureCanAdd(HabitProgress.MaxEntriesPerDay).Error);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void EnsureCanRecordProgress_ShouldAcceptTodayAndYesterday(int daysAgo)
        {
            Assert.True(Stored().EnsureCanRecordProgress(Today.AddDays(-daysAgo), Today).IsSuccess);
        }

        [Fact]
        public void EnsureCanRecordProgress_ShouldReject_WhenHabitHasNoTarget()
        {
            Assert.Equal(HabitErrors.NotMeasurable, Stored(targetValue: null).EnsureCanRecordProgress(Today, Today).Error);
        }

        [Fact]
        public void EnsureCanRecordProgress_ShouldReject_WhenHabitIsArchived()
        {
            Assert.Equal(HabitErrors.Archived, Stored(archivedAt: Now).EnsureCanRecordProgress(Today, Today).Error);
        }

        [Fact]
        public void EnsureCanRecordProgress_ShouldReject_WhenHabitIsToAvoid()
        {
            Assert.Equal(HabitErrors.NotCheckable, Stored(kind: HabitKind.Negative, targetValue: null).EnsureCanRecordProgress(Today, Today).Error);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(-1)]
        public void EnsureCanRecordProgress_ShouldReject_WhenDayIsOutsideTheWindow(int daysAgo)
        {
            Assert.Equal(HabitErrors.CheckInOutsideWindow, Stored().EnsureCanRecordProgress(Today.AddDays(-daysAgo), Today).Error);
        }

        [Fact]
        public void EnsureCanRecordProgress_ShouldReject_WhenDayIsNotScheduled()
        {
            var mondays = Stored(frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Monday);

            Assert.Equal(HabitErrors.NotScheduled, mondays.EnsureCanRecordProgress(Today, Today).Error);
        }
    }
}
