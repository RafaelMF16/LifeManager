using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Application.Habits.DTOs
{
    /// <summary><c>GET /api/Habits/{id}/Stats</c>: the habit's details screen.</summary>
    /// <param name="Today">The backend's today (<c>yyyy-MM-dd</c>), the heatmap's last day.</param>
    /// <param name="Days">The heatmap's days (<see cref="HabitStats.HeatmapDays"/>), oldest first.</param>
    /// <param name="TotalKept">Days done (or clean, to avoid) since the habit started.</param>
    public record HabitStatsDto(
        HabitResponseDto Habit,
        DateOnly Today,
        IReadOnlyList<HabitDayDto> Days,
        HabitConsistencyDto Consistency,
        int TotalKept);

    public record HabitDayDto(DateOnly Date, HabitDayState State);

    /// <param name="Percent">Null while nothing was judged yet.</param>
    public record HabitConsistencyDto(int Achieved, int Due, int? Percent, HabitConsistencyUnit Unit)
    {
        public static HabitConsistencyDto From(HabitConsistency consistency)
            => new(consistency.Achieved, consistency.Due, consistency.Percent, consistency.Unit);
    }
}
