using LifeManager.Domain.Rewards;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class RewardSingleton : List<Reward>
    {
        private RewardSingleton() { }

        private static readonly Lazy<RewardSingleton> lazy = new(() => new RewardSingleton());

        public static RewardSingleton Instance => lazy.Value;
    }
}
