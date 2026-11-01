using LifeManager.Domain.Ranking;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class RankingWeekResultSingleton : List<RankingWeekResult>
    {
        private RankingWeekResultSingleton() { }

        private static readonly Lazy<RankingWeekResultSingleton> lazy = new(() => new RankingWeekResultSingleton());

        public static RankingWeekResultSingleton Instance => lazy.Value;
    }
}
