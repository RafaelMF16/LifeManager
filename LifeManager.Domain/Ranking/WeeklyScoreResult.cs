namespace LifeManager.Domain.Ranking
{
    /// <param name="Kept">Habits done or clean that day, capped at <see cref="RankingRules.MaxKeptHabitsPerDay"/>.</param>
    public record WeeklyScoreDay(DateOnly Date, int Kept, bool Perfect, int Points);

    /// <param name="KeptDays">The capped kept habits of the whole week (a tiebreaker after the perfect days).</param>
    public record WeeklyScoreResult(DateOnly WeekStart, int Points, int KeptDays, int PerfectDays, IReadOnlyList<WeeklyScoreDay> Days)
    {
        public static WeeklyScoreResult Empty(DateOnly weekStart)
            => WeeklyScore.Calculate([], [], weekStart, weekStart.AddDays(-1));
    }
}
