namespace LifeManager.Domain.Ranking
{
    /// <summary>Every number of the ranking, in one place (like <c>GameRules</c> for the habits game).</summary>
    public static class RankingRules
    {
        /// <summary>Points for each habit done or clean on a day.</summary>
        public const int PointsPerKeptHabit = 10;

        /// <summary>Kept habits that score on one day; more habits don't score more.</summary>
        public const int MaxKeptHabitsPerDay = 5;

        /// <summary>Bonus for a day on which every due habit to build was done and no habit to avoid relapsed.</summary>
        public const int PerfectDayBonus = 10;

        /// <summary>Points for a <c>Steady</c> or <c>Stretch</c> weekly challenge whose target the week reached.</summary>
        public const int PointsPerChallenge = 15;

        /// <summary>Challenges that score in one week (a week takes at most 3 anyway).</summary>
        public const int MaxScoredChallengesPerWeek = 3;

        /// <summary>Ranking groups a player can be in (created or joined).</summary>
        public const int MaxGroupsPerUser = 5;

        public const int MaxMembersPerGroup = 20;

        /// <summary>Members in from a week's Monday that a group needs for the week to count for achievements.</summary>
        public const int MinEligibleMembers = 3;

        /// <summary>
        /// Days after the usual close (the Tuesday) that a week waits for every member's day close; past them it closes
        /// with what is stored, so one player whose day close keeps failing doesn't hold the group back.
        /// </summary>
        public const int CloseGraceDays = 3;

        /// <summary>The podium's last position.</summary>
        public const int PodiumPositions = 3;
    }
}
