using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Ranking
{
    /// <summary>
    /// A player's ranking points in a week, from the stored check-ins only. It measures constancy: neither the habits'
    /// difficulty nor having more than <see cref="RankingRules.MaxKeptHabitsPerDay"/> habits scores more.
    /// </summary>
    public static class WeeklyScore
    {
        /// <param name="habits">Every habit of the player, archived ones included.</param>
        /// <param name="checkIns">The player's check-ins; those outside the week are ignored.</param>
        /// <param name="upTo">Days after it score nothing (today for the live score, yesterday to compare).</param>
        public static WeeklyScoreResult Calculate(
            IReadOnlyCollection<ScoredHabit> habits,
            IReadOnlyCollection<ScoredCheckIn> checkIns,
            DateOnly weekStart,
            DateOnly upTo)
        {
            var byDate = checkIns.ToLookup(checkIn => checkIn.Date);

            var days = Enumerable.Range(0, GameWeek.Days)
                .Select(weekStart.AddDays)
                .Select(date => date > upTo ? new WeeklyScoreDay(date, 0, false, 0) : ScoreDay(date, habits, byDate[date].ToList()))
                .ToList();

            return new WeeklyScoreResult(
                weekStart,
                days.Sum(day => day.Points),
                days.Sum(day => day.Kept),
                days.Count(day => day.Perfect),
                days);
        }

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
