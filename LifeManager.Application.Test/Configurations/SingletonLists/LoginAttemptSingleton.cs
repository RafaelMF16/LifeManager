using LifeManager.Domain.Auth;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class LoginAttemptSingleton : List<LoginAttempt>
    {
        private LoginAttemptSingleton() { }

        private static readonly Lazy<LoginAttemptSingleton> lazy = new(() => new LoginAttemptSingleton());

        public static LoginAttemptSingleton Instance => lazy.Value;
    }
}
