using LifeManager.Domain.Auth;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class PasswordResetTokenSingleton : List<PasswordResetToken>
    {
        private PasswordResetTokenSingleton() { }

        private static readonly Lazy<PasswordResetTokenSingleton> lazy = new(() => new PasswordResetTokenSingleton());

        public static PasswordResetTokenSingleton Instance => lazy.Value;
    }
}
