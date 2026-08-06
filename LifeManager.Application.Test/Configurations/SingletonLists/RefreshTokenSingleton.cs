using LifeManager.Domain.Auth;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class RefreshTokenSingleton : List<RefreshToken>
    {
        private RefreshTokenSingleton() { }

        private static readonly Lazy<RefreshTokenSingleton> lazy = new(() => new RefreshTokenSingleton());

        public static RefreshTokenSingleton Instance => lazy.Value;
    }
}
