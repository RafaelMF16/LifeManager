using LifeManager.Domain.Users;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class UserSingleton : List<User>
    {
        private UserSingleton() { }

        private static readonly Lazy<UserSingleton> lazy = new(() => new UserSingleton());

        public static UserSingleton Instance => lazy.Value;
    }
}