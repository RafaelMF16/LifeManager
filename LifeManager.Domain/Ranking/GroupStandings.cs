using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Ranking
{
    public record Standing(UserId UserId, int Points, int PerfectDays, int KeptDays, int Position);

    /// <summary>A group's positions in a week, and what the ranking tells each member about them. Pure.</summary>
    public static class GroupStandings
    {
        /// <summary>
        /// Best first: points, then perfect days, then kept habits. A tie on all three shares the position and the next
        /// one skips (1, 1, 3).
        /// </summary>
        public static IReadOnlyList<Standing> Rank(IEnumerable<(UserId UserId, WeeklyScoreResult Score)> scores)
        {
            var ordered = scores
                .OrderByDescending(score => score.Score.Points)
                .ThenByDescending(score => score.Score.PerfectDays)
                .ThenByDescending(score => score.Score.KeptDays)
                .ToList();

            var standings = new List<Standing>(ordered.Count);
            for (var i = 0; i < ordered.Count; i++)
            {
                var (userId, score) = ordered[i];
                var position = i > 0 && SameRank(ordered[i - 1].Score, score) ? standings[i - 1].Position : i + 1;
                standings.Add(new Standing(userId, score.Points, score.PerfectDays, score.KeptDays, position));
            }

            return standings;
        }

        /// <summary>A member counts for the week's achievements only when they were in from its Monday.</summary>
        public static bool IsEligible(DateOnly joinedOn, DateOnly weekStart)
            => joinedOn <= weekStart;

        public static bool CountsForAchievements(int eligibleCount)
            => eligibleCount >= RankingRules.MinEligibleMembers;

        /// <summary>
        /// Who is ahead of <paramref name="me"/> now but was behind or tied with them in <paramref name="before"/> (the
        /// ranking up to yesterday). Only members in both rankings.
        /// </summary>
        public static IReadOnlyList<UserId> OvertakenBy(IReadOnlyList<Standing> before, IReadOnlyList<Standing> now, UserId me)
        {
            var mineBefore = before.SingleOrDefault(standing => standing.UserId == me);
            var mineNow = now.SingleOrDefault(standing => standing.UserId == me);
            if (mineBefore is null || mineNow is null)
                return [];

            var positionsBefore = before.ToDictionary(standing => standing.UserId, standing => standing.Position);

            return [.. now
                .Where(standing => standing.UserId != me
                    && standing.Position < mineNow.Position
                    && positionsBefore.TryGetValue(standing.UserId, out var positionBefore)
                    && positionBefore >= mineBefore.Position)
                .Select(standing => standing.UserId)];
        }

        /// <summary>The member right ahead of <paramref name="me"/> (the closest of those ranked better); null when first.</summary>
        public static Standing? Next(IReadOnlyList<Standing> standings, UserId me)
        {
            var mine = standings.SingleOrDefault(standing => standing.UserId == me);
            if (mine is null)
                return null;

            return standings.LastOrDefault(standing => standing.Position < mine.Position);
        }

        private static bool SameRank(WeeklyScoreResult a, WeeklyScoreResult b)
            => a.Points == b.Points && a.PerfectDays == b.PerfectDays && a.KeptDays == b.KeptDays;
    }
}
