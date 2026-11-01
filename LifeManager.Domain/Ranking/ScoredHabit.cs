using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;

namespace LifeManager.Domain.Ranking
{
    /// <summary>What the weekly score needs to know about a habit: whether it was due on a day.</summary>
    /// <param name="ArchivedOn">The business day it was archived on; it is still due that day.</param>
    public record ScoredHabit(int HabitId, HabitKind Kind, HabitFrequency Frequency, DateOnly StartDate, DateOnly? ArchivedOn)
    {
        /// <summary>
        /// Whether the habit counts for the perfect day on <paramref name="date"/>. A times-per-week habit has no fixed
        /// day, so it never does.
        /// </summary>
        public bool IsDueOn(DateOnly date)
            => Frequency.Type != HabitFrequencyType.TimesPerWeek
                && date >= StartDate
                && (ArchivedOn is null || date <= ArchivedOn)
                && Frequency.IsScheduledOn(date);
    }

    public record ScoredCheckIn(int HabitId, DateOnly Date, HabitCheckInStatus Status);
}
