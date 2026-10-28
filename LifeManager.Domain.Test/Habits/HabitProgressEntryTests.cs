using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;

namespace LifeManager.Domain.Test.Habits
{
    public class HabitProgressEntryTests
    {
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateTimeOffset Now = new(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);

        private static readonly Habit Habit = Domain.Habits.Habit.FromPersistence(
            7, 3, "Drink water", null, null, HabitKind.Positive, HabitDifficulty.Easy, HabitFrequencyType.Daily, HabitWeekDays.None, null,
            Today, Now, archivedAt: null, currentStreak: 0, longestStreak: 0, evaluatedUntil: Today.AddDays(-1),
            targetValue: 2000, targetUnit: "ml", targetStep: 250);

        [Theory]
        [InlineData(1)]
        [InlineData(HabitProgressEntry.MaxValue)]
        public void Create_ShouldKeepTheHabitUserDayAndValue(int value)
        {
            var entry = HabitProgressEntry.Create(Habit, Today, value, Now).Value!;

            Assert.Equal(Habit.Id, entry.HabitId);
            Assert.Equal(Habit.UserId, entry.UserId);
            Assert.Equal(Today, entry.Date);
            Assert.Equal(value, entry.Value);
            Assert.Equal(Now, entry.CreatedAt);
            Assert.Null(entry.Id);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-250)]
        [InlineData(HabitProgressEntry.MaxValue + 1)]
        public void Create_ShouldReturnFailure_WhenValueIsOutOfRange(int value)
        {
            Assert.Equal(HabitErrors.InvalidProgressValue, HabitProgressEntry.Create(Habit, Today, value, Now).Error);
        }
    }
}
