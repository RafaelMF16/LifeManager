using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>Body of <c>PUT /api/Habits/{id}</c>: everything but the kind, which is fixed once created.</summary>
    /// <param name="TargetValue">Null (with no unit or step) removes the daily target.</param>
    /// <param name="RelapseCost">Null removes the cost per relapse.</param>
    /// <param name="AutoTrigger">Left out = <see cref="HabitAutoTrigger.None"/>.</param>
    /// <param name="ReminderHour">Left out (null) removes the reminder.</param>
    public record HabitUpdateDto(
        string Name,
        string? Description,
        string? Trigger,
        HabitDifficulty Difficulty,
        HabitFrequencyType FrequencyType,
        IReadOnlyList<DayOfWeek>? WeekDays,
        int? TimesPerWeek,
        int? TargetValue = null,
        string? TargetUnit = null,
        int? TargetStep = null,
        decimal? RelapseCost = null,
        HabitAutoTrigger AutoTrigger = HabitAutoTrigger.None,
        int? ReminderHour = null);
}
