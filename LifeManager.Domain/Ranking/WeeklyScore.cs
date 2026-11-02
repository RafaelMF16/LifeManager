using LifeManager.Domain.Habits.Challenges;
using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Ranking
{
    /// <summary>
    /// A player's ranking points in a week, from the stored check-ins only. It measures constancy: neither the habits'
    /// difficulty nor having more than <see cref="RankingRules.MaxKeptHabitsPerDay"/> habits scores more. On top, each
    /// weekly challenge that asked for more than usual and was reached adds <see cref="RankingRules.PointsPerChallenge"/>.
    /// </summary>
    public static class WeeklyScore
    {
        /// <param name="habits">Every habit of the player, archived ones included.</param>
        /// <param name="checkIns">The player's check-ins; those outside the week are ignored.</param>
        /// <param name="upTo">Days after it score nothing (today for the live score, yesterday to compare).</param>
        /// <param name="challenges">The player's challenges of the week.</param>
        /// <param name="progress">The amounts logged in the week, for the volume challenges.</param>
        public static WeeklyScoreResult Calculate(
            IReadOnlyCollection<ScoredHabit> habits,
            IReadOnlyCollection<ScoredCheckIn> checkIns,
            DateOnly weekStart,
            DateOnly upTo,
            IReadOnlyCollection<ScoredChallenge>? challenges = null,
            IReadOnlyCollection<ScoredProgress>? progress = null)
        {
            var byDate = checkIns.ToLookup(checkIn => checkIn.Date);

            var days = Enumerable.Range(0, GameWeek.Days)
                .Select(weekStart.AddDays)
                .Select(date => date > upTo ? new WeeklyScoreDay(date, 0, false, 0) : ScoreDay(date, habits, byDate[date].ToList()))
                .ToList();

            var challengePoints = ChallengePoints(challenges ?? [], checkIns, progress ?? [], weekStart, upTo);

            return new WeeklyScoreResult(
                weekStart,
                days.Sum(day => day.Points) + challengePoints,
                days.Sum(day => day.Kept),
                days.Count(day => day.Perfect),
                days,
                challengePoints);
        }

        /// <summary>
        /// The challenges' bonus: a <c>Steady</c> or <c>Stretch</c> one, active or completed, whose progress recounted up to
        /// <paramref name="upTo"/> reaches its target, up to <see cref="RankingRules.MaxScoredChallengesPerWeek"/>. It reads
        /// what is stored, not the status: undoing the check-in that completed a challenge takes its points back, even if
        /// the coins stay paid.
        /// </summary>
        public static int ChallengePoints(
            IReadOnlyCollection<ScoredChallenge> challenges,
            IReadOnlyCollection<ScoredCheckIn> checkIns,
            IReadOnlyCollection<ScoredProgress> progress,
            DateOnly weekStart,
            DateOnly upTo)
        {
            var reached = challenges.Count(challenge =>
                challenge.Effort is ChallengeEffort.Steady or ChallengeEffort.Stretch
                && challenge.Status is ChallengeStatus.Active or ChallengeStatus.Completed
                && Progress(challenge, checkIns, progress, weekStart, upTo) >= challenge.Target);

            return Math.Min(reached, RankingRules.MaxScoredChallengesPerWeek) * RankingRules.PointsPerChallenge;
        }

        private static int Progress(
            ScoredChallenge challenge, IReadOnlyCollection<ScoredCheckIn> checkIns, IReadOnlyCollection<ScoredProgress> progress,
            DateOnly weekStart, DateOnly upTo)
            => challenge.Type == ChallengeType.Volume
                ? ChallengeProgress.Volume(weekStart, progress
                    .Where(entry => entry.HabitId == challenge.HabitId && entry.Date <= upTo)
                    .Select(entry => (entry.Date, entry.Value)))
                : ChallengeProgress.Frequency(weekStart, checkIns
                    .Where(checkIn => checkIn.HabitId == challenge.HabitId && checkIn.Date <= upTo)
                    .Select(checkIn => (checkIn.Date, checkIn.Status)));

        private static WeeklyScoreDay ScoreDay(DateOnly date, IReadOnlyCollection<ScoredHabit> habits, IReadOnlyList<ScoredCheckIn> dayCheckIns)
        {
            var statusByHabit = dayCheckIns.ToDictionary(checkIn => checkIn.HabitId, checkIn => checkIn.Status);

            var kept = Math.Min(
                dayCheckIns.Count(checkIn => checkIn.Status is HabitCheckInStatus.Done or HabitCheckInStatus.Clean),
                RankingRules.MaxKeptHabitsPerDay);

            // A frozen day is protected: it neither completes nor breaks the perfect day.
            var due = habits
                .Where(habit => habit.IsDueOn(date))
                .Where(habit => !statusByHabit.TryGetValue(habit.HabitId, out var status) || status != HabitCheckInStatus.Frozen)
                .ToList();

            var perfect = due.Count > 0 && due.All(habit => IsKeptOn(habit, statusByHabit));

            return new WeeklyScoreDay(
                date,
                kept,
                perfect,
                kept * RankingRules.PointsPerKeptHabit + (perfect ? RankingRules.PerfectDayBonus : 0));
        }

        private static bool IsKeptOn(ScoredHabit habit, IReadOnlyDictionary<int, HabitCheckInStatus> statusByHabit)
        {
            statusByHabit.TryGetValue(habit.HabitId, out var status);

            // To build: must be done. To avoid: anything but a relapse (the clean day is only written by the day close).
            return habit.Kind == HabitKind.Positive
                ? status == HabitCheckInStatus.Done
                : status != HabitCheckInStatus.Relapse;
        }
    }
}
