using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Application.Habits.DTOs
{
    /// <summary><c>GET /api/Habits/Today</c>: the day's checklist, plus yesterday's habits that can still be checked in.</summary>
    /// <param name="Date">Today, in the business time zone.</param>
    /// <param name="LastMissedOn">
    /// The latest day an active habit was missed in the last <c>HabitCheckInService.RecentMissDays</c> days; null when
    /// none. The frontend welcomes the player back after a miss.
    /// </param>
    /// <param name="RecentMissCount">How many missed habit-days those were.</param>
    public record HabitTodayDto(
        DateOnly Date,
        IReadOnlyList<HabitTodayItemDto> Today,
        IReadOnlyList<HabitTodayItemDto> YesterdayPending,
        DateOnly? LastMissedOn,
        int RecentMissCount);

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
