using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>Body of <c>POST /api/Habits</c>.</summary>
    /// <param name="WeekDays">Only for <see cref="HabitFrequencyType.WeekDays"/>, e.g. <c>["Monday", "Thursday"]</c>.</param>
    /// <param name="TimesPerWeek">Only for <see cref="HabitFrequencyType.TimesPerWeek"/>.</param>
    /// <param name="TargetValue">The daily target of a habit to build; null (with no unit or step) for a one-tap habit.</param>
    /// <param name="RelapseCost">What one relapse of a habit to avoid costs in money; null without one.</param>
    /// <param name="AutoTrigger">The finance action that checks a one-tap habit to build in by itself.</param>
    /// <param name="ReminderHour">The hour (0–23) of a habit to build's reminder; null without one.</param>
    public record HabitDto(
        string Name,
        string? Description,
        string? Trigger,
        HabitKind Kind,
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
