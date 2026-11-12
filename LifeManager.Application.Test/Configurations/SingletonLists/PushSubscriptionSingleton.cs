using LifeManager.Domain.Push;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class PushSubscriptionSingleton : List<PushSubscription>
    {
        private PushSubscriptionSingleton() { }

        private static readonly Lazy<PushSubscriptionSingleton> lazy = new(() => new PushSubscriptionSingleton());

        public static PushSubscriptionSingleton Instance => lazy.Value;
    }
}
