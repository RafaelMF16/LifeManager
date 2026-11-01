using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Ranking.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Ranking
{
    /// <summary>
    /// A member's final line in a group's week, stored by the week close. It never changes afterwards: an undone check-in
    /// or someone leaving the group doesn't rewrite the past, so the podium and the achievements built on it stay put.
    /// </summary>
    public class RankingWeekResult
    {
        public RankingWeekResultId? Id { get; private set; }
        public RankingGroupId GroupId { get; }
        public DateOnly WeekStart { get; }
        public UserId UserId { get; }
        public int Points { get; }
        public int PerfectDays { get; }
        public int KeptDays { get; }
        public int Position { get; }

        /// <summary>In the group from the week's Monday: only then can the week give them achievements.</summary>
        public bool Eligible { get; }

        public int MemberCount { get; }
        public int EligibleCount { get; }
        public DateTimeOffset ClosedAt { get; }

        private RankingWeekResult(
            RankingGroupId groupId, DateOnly weekStart, UserId userId, int points, int perfectDays, int keptDays, int position,
            bool eligible, int memberCount, int eligibleCount, DateTimeOffset closedAt)
        {
            GroupId = groupId;
            WeekStart = weekStart;
            UserId = userId;
            Points = points;
            PerfectDays = perfectDays;
            KeptDays = keptDays;
            Position = position;
            Eligible = eligible;
            MemberCount = memberCount;
            EligibleCount = eligibleCount;
            ClosedAt = closedAt;
        }

        /// <summary>One line per member: the standings plus who was in from Monday.</summary>
        /// <param name="joinedOn">Each member's join day.</param>
        public static IReadOnlyList<RankingWeekResult> ForWeek(
            RankingGroupId groupId,
            DateOnly weekStart,
            IReadOnlyList<Standing> standings,
            IReadOnlyDictionary<UserId, DateOnly> joinedOn,
            DateTimeOffset closedAt)
        {
            var eligibleCount = standings.Count(standing => GroupStandings.IsEligible(joinedOn[standing.UserId], weekStart));

            return [.. standings.Select(standing => new RankingWeekResult(
                groupId, weekStart, standing.UserId, standing.Points, standing.PerfectDays, standing.KeptDays, standing.Position,
                GroupStandings.IsEligible(joinedOn[standing.UserId], weekStart), standings.Count, eligibleCount, closedAt))];
        }

        /// <summary>
        /// Whether this line can give the player a ranking achievement: in from Monday, in a week of at least
        /// <see cref="RankingRules.MinEligibleMembers"/> such members, and with points (an empty week wins nothing).
        /// Achievements then count distinct weeks, so the same week won in two groups counts once.
        /// </summary>
        public bool CountsForAchievements
            => Eligible && GroupStandings.CountsForAchievements(EligibleCount) && Points > 0;

        public bool IsWin
            => CountsForAchievements && Position == 1;

        public bool IsPodium
            => CountsForAchievements && Position <= RankingRules.PodiumPositions;

        /// <summary>
        /// Whether the week closes even with a member's day close behind: <see cref="RankingRules.CloseGraceDays"/> days
        /// past the Tuesday it usually closes on.
        /// </summary>
        public static bool IsPastGrace(DateOnly weekStart, DateOnly today)
            => today > GameWeek.End(weekStart).AddDays(2 + RankingRules.CloseGraceDays);

        public void AssignId(int id)
        {
            Id = new RankingWeekResultId(id);
        }

        internal static RankingWeekResult FromPersistence(
            int id, int idGroup, DateOnly weekStart, int idUser, int points, int perfectDays, int keptDays, int position,
            bool eligible, int memberCount, int eligibleCount, DateTimeOffset closedAt)
        {
            var result = new RankingWeekResult(
                new RankingGroupId(idGroup), weekStart, new UserId(idUser), points, perfectDays, keptDays, position,
                eligible, memberCount, eligibleCount, closedAt);
            result.AssignId(id);

            return result;
        }
    }
}
