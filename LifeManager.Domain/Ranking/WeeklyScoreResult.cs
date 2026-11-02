namespace LifeManager.Domain.Ranking
{
    /// <param name="Kept">Habits done or clean that day, capped at <see cref="RankingRules.MaxKeptHabitsPerDay"/>.</param>
    public record WeeklyScoreDay(DateOnly Date, int Kept, bool Perfect, int Points);

    /// <param name="Points">The days' points plus <paramref name="ChallengePoints"/>.</param>
    /// <param name="KeptDays">The capped kept habits of the whole week (a tiebreaker after the perfect days).</param>
    /// <param name="ChallengePoints">The weekly challenges' bonus, already in <paramref name="Points"/>.</param>
    public record WeeklyScoreResult(
        DateOnly WeekStart, int Points, int KeptDays, int PerfectDays, IReadOnlyList<WeeklyScoreDay> Days, int ChallengePoints = 0)
    {
        public static WeeklyScoreResult Empty(DateOnly weekStart)
            => WeeklyScore.Calculate([], [], weekStart, weekStart.AddDays(-1));
    }
}
