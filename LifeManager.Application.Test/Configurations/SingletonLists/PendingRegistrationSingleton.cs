using LifeManager.Domain.Auth;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class PendingRegistrationSingleton : List<PendingRegistration>
    {
        private PendingRegistrationSingleton() { }

        private static readonly Lazy<PendingRegistrationSingleton> lazy = new(() => new PendingRegistrationSingleton());

        public static PendingRegistrationSingleton Instance => lazy.Value;
    }
}
