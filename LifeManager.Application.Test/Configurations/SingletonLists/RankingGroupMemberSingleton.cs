using LifeManager.Domain.RankingGroups;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class RankingGroupMemberSingleton : List<RankingGroupMember>
    {
        private RankingGroupMemberSingleton() { }

        private static readonly Lazy<RankingGroupMemberSingleton> lazy = new(() => new RankingGroupMemberSingleton());

        public static RankingGroupMemberSingleton Instance => lazy.Value;
    }
}
