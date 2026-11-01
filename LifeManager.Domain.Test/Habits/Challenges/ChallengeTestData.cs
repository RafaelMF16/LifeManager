using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Test.Habits.Challenges
{
    internal static class ChallengeTestData
    {
        // Monday 2026-11-02 to Sunday 2026-11-08.
        public static readonly DateOnly Monday = new(2026, 11, 2);
        public static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

        public static Habit Habit(
            HabitKind kind = HabitKind.Positive,
            HabitDifficulty difficulty = HabitDifficulty.Medium,
            HabitFrequencyType frequencyType = HabitFrequencyType.Daily,
            HabitWeekDays weekDays = HabitWeekDays.None,
            int? timesPerWeek = null,
            DateOnly? startDate = null,
            DateTimeOffset? archivedAt = null,
            int? targetValue = null,
            int? targetStep = null)
            => Domain.Habits.Habit.FromPersistence(
                7, 1, "Ler", null, null, kind, difficulty, frequencyType, weekDays, timesPerWeek,
                startDate ?? Monday.AddDays(-60), Now.AddDays(-60), archivedAt, 0, 0, Monday.AddDays(-1),
                targetValue, targetValue is null ? null : "ml", targetStep);
    }
}
