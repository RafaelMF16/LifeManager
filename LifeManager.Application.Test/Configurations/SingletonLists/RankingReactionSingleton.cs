using LifeManager.Domain.Ranking;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class RankingReactionSingleton : List<RankingReaction>
    {
        private RankingReactionSingleton() { }

        private static readonly Lazy<RankingReactionSingleton> lazy = new(() => new RankingReactionSingleton());

        public static RankingReactionSingleton Instance => lazy.Value;
    }
}
