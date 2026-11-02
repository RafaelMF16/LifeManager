namespace LifeManager.Domain.Ranking.Enums
{
    public enum RankingWeekStatus
    {
        /// <summary>The current week: live points.</summary>
        Open = 1,

        /// <summary>Over, but not stored yet: points can still change until every member's day close judged it.</summary>
        Closing = 2,

        /// <summary>Stored by the week close; never changes again.</summary>
        Final = 3
    }
}
