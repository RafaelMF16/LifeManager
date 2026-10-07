using LifeManager.Domain.Habits;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class PlayerProfileSingleton : List<PlayerProfile>
    {
        private PlayerProfileSingleton() { }

        private static readonly Lazy<PlayerProfileSingleton> lazy = new(() => new PlayerProfileSingleton());

        public static PlayerProfileSingleton Instance => lazy.Value;
    }
}
