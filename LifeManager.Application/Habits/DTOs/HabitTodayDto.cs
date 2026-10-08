using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Application.Habits.DTOs
{
    /// <summary><c>GET /api/Habits/Today</c>: the day's checklist, plus yesterday's habits that can still be checked in.</summary>
    /// <param name="Date">Today, in the business time zone.</param>
    /// <param name="LastMissedOn">
    /// The latest day an active habit was missed in the last <c>HabitCheckInService.RecentMissDays</c> days; null when
    /// none. The frontend welcomes the player back after a miss.
    /// </param>
    /// <param name="RecentMissCount">How many missed habit-days (relapses included) those were.</param>
    /// <param name="Avoiding">Habits to avoid that are in force today (a weekly limit always is).</param>
    /// <param name="FreeToday">Names of set-days habits to avoid whose day off is today.</param>
    public record HabitTodayDto(
        DateOnly Date,
        IReadOnlyList<HabitTodayItemDto> Today,
        IReadOnlyList<HabitTodayItemDto> YesterdayPending,
        DateOnly? LastMissedOn,
        int RecentMissCount,
        IReadOnlyList<HabitAvoidItemDto> Avoiding,
        IReadOnlyList<string> FreeToday);

    /// <summary>A habit to avoid on the day's checklist.</summary>
    /// <param name="CurrentStreak">Clean days in a row (weeks within the limit, for a weekly limit).</param>
    /// <param name="CanRelapseYesterday">Whether yesterday was a day it was avoided, can still change, and has no relapse.</param>
    /// <param name="WeekRelapseCount">Weekly limit only: relapses logged this week (Monday to Sunday).</param>
    /// <param name="DamagePreview">HP a relapse today would cost: 0 while still within a weekly limit, or once relapsed today.</param>
    public record HabitAvoidItemDto(
        int Id,
        string Name,
        string? Trigger,
        HabitDifficulty Difficulty,
        HabitFrequencyType FrequencyType,
        int? TimesPerWeek,
        int CurrentStreak,
        int LongestStreak,
        bool RelapsedToday,
        bool RelapsedYesterday,
        bool CanRelapseYesterday,
        int? WeekRelapseCount,
        int DamagePreview);

    /// <param name="Done">Whether the habit is checked in on the day of the list it is in.</param>
    /// <param name="WeekDoneCount">Times-per-week habits only: check-ins in that day's week (Monday to Sunday).</param>
    /// <param name="CoinsPreview">
    /// Coins a check-in would earn now (0 once a times-per-week target is met); an estimate from the stored streak.
    /// </param>
    public record HabitTodayItemDto(
        int Id,
        string Name,
        string? Trigger,
        HabitDifficulty Difficulty,
        HabitFrequencyType FrequencyType,
        int? TimesPerWeek,
        int CurrentStreak,
        int LongestStreak,
        bool Done,
        int? WeekDoneCount,
        int CoinsPreview);
}
