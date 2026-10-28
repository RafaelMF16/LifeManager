using LifeManager.Domain.Achievements;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class AchievementUnlockSingleton : List<AchievementUnlock>
    {
        private AchievementUnlockSingleton() { }

        private static readonly Lazy<AchievementUnlockSingleton> lazy = new(() => new AchievementUnlockSingleton());

        public static AchievementUnlockSingleton Instance => lazy.Value;
    }
}
