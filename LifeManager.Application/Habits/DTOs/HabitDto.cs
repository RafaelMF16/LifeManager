using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>Body of <c>POST /api/Habits</c>.</summary>
    /// <param name="WeekDays">Only for <see cref="HabitFrequencyType.WeekDays"/>, e.g. <c>["Monday", "Thursday"]</c>.</param>
    /// <param name="TimesPerWeek">Only for <see cref="HabitFrequencyType.TimesPerWeek"/>.</param>
    /// <param name="TargetValue">The daily target of a habit to build; null (with no unit or step) for a one-tap habit.</param>
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
        int? TargetStep = null);
}
