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

    /// <summary>An amount logged toward a habit's daily target, for a volume challenge's progress.</summary>
    public record ScoredProgress(int HabitId, DateOnly Date, int Value);

    /// <summary>A weekly challenge as the score reads it; its progress is recounted from the check-ins and amounts.</summary>
    public record ScoredChallenge(int HabitId, ChallengeType Type, int Target, ChallengeEffort Effort, ChallengeStatus Status);
}
