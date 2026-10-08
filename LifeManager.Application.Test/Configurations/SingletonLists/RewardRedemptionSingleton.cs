using LifeManager.Domain.Rewards;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class RewardRedemptionSingleton : List<RewardRedemption>
    {
        private RewardRedemptionSingleton() { }

        private static readonly Lazy<RewardRedemptionSingleton> lazy = new(() => new RewardRedemptionSingleton());

        public static RewardRedemptionSingleton Instance => lazy.Value;
    }
}
