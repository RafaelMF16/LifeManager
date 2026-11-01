using LifeManager.Domain.RankingGroups;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class RankingGroupSingleton : List<RankingGroup>
    {
        private RankingGroupSingleton() { }

        private static readonly Lazy<RankingGroupSingleton> lazy = new(() => new RankingGroupSingleton());

        public static RankingGroupSingleton Instance => lazy.Value;
    }
}
