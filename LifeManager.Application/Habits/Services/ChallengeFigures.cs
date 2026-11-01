using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Challenges;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Ranking;

namespace LifeManager.Application.Habits.Services
{
    /// <summary>The stored check-ins and progress entries of a date range, counted the way the challenges count them.</summary>
    internal class ChallengeFigures(IReadOnlyList<HabitCheckIn> checkIns, IReadOnlyList<HabitProgressEntry> entries)
    {
        private readonly ILookup<HabitId, HabitCheckIn> _checkIns = checkIns.ToLookup(checkIn => checkIn.HabitId);
        private readonly ILookup<HabitId, HabitProgressEntry> _entries = entries.ToLookup(entry => entry.HabitId);

        public int Frequency(HabitId habitId, DateOnly weekStart)
            => ChallengeProgress.Frequency(weekStart, _checkIns[habitId].Select(checkIn => (checkIn.Date, checkIn.Status)));

        public int Volume(HabitId habitId, DateOnly weekStart)
            => ChallengeProgress.Volume(weekStart, _entries[habitId].Select(entry => (entry.Date, entry.Value)));

        public IReadOnlyCollection<DateOnly> DoneDates(HabitId habitId, DateOnly weekStart)
            => [.. _checkIns[habitId]
                .Where(checkIn => checkIn.Status == HabitCheckInStatus.Done && checkIn.Date >= weekStart && checkIn.Date <= GameWeek.End(weekStart))
                .Select(checkIn => checkIn.Date)];

        public int Progress(WeeklyChallenge challenge)
            => challenge.Type == ChallengeType.Volume
                ? Volume(challenge.HabitId, challenge.WeekStart)
                : Frequency(challenge.HabitId, challenge.WeekStart);

        /// <summary>The habit's weekly average over the closed weeks before <paramref name="today"/>'s; null without any.</summary>
        public decimal? Baseline(Habit habit, ChallengeType type, DateOnly today)
            => ChallengeRules.Baseline([.. ChallengeRules.ClosedWeeks(today, habit.StartDate)
                .Select(weekStart => type == ChallengeType.Volume ? Volume(habit.Id!, weekStart) : Frequency(habit.Id!, weekStart))]);
    }
}
