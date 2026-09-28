using LifeManager.Domain.UsersPreferences;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class UserPreferencesSingleton : List<UserPreferences>
    {
        private UserPreferencesSingleton() { }

        private static readonly Lazy<UserPreferencesSingleton> lazy = new(() => new UserPreferencesSingleton());

        public static UserPreferencesSingleton Instance => lazy.Value;
    }
}
