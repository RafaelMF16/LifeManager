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
        DateTimeOffset? ArchivedAt);
}
