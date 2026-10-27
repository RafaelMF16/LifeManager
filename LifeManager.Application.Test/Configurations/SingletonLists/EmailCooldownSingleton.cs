using LifeManager.Domain.Auth;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class EmailCooldownSingleton : List<EmailCooldown>
    {
        private EmailCooldownSingleton() { }

        private static readonly Lazy<EmailCooldownSingleton> lazy = new(() => new EmailCooldownSingleton());

        public static EmailCooldownSingleton Instance => lazy.Value;
    }
}
