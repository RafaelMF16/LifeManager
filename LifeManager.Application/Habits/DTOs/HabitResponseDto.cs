using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Application.Habits.DTOs
{
    /// <param name="WeekDays">Monday first; empty unless the frequency is <see cref="HabitFrequencyType.WeekDays"/>.</param>
    /// <param name="ArchivedAt">Null while the habit is active.</param>
    public record HabitResponseDto(
        int Id,
        string Name,
        string? Description,
        string? Trigger,
        HabitKind Kind,
        HabitDifficulty Difficulty,
        HabitFrequencyType FrequencyType,
        IReadOnlyList<DayOfWeek> WeekDays,
        int? TimesPerWeek,
        DateOnly StartDate,
        int CurrentStreak,
        int LongestStreak,
        DateTimeOffset? ArchivedAt)
    {
        public static HabitResponseDto From(Habit habit)
            => new(
                habit.Id!.Value,
                habit.Name.Value,
                habit.Description?.Value,
                habit.Trigger?.Value,
                habit.Kind,
                habit.Difficulty,
                habit.FrequencyType,
                HabitWeekDaysMapper.ToDays(habit.WeekDays),
                habit.TimesPerWeek,
                habit.StartDate,
                habit.CurrentStreak,
                habit.LongestStreak,
                habit.ArchivedAt);
    }
}
