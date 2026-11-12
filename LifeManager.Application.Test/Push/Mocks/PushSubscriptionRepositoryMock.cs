using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Push;
using LifeManager.Domain.Push.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Push.Mocks
{
    /// <summary>Mirrors the upsert by endpoint (owner change included) and the per-user limit of the real repository.</summary>
    public class PushSubscriptionRepositoryMock : IPushSubscriptionRepository
    {
        private readonly PushSubscriptionSingleton _instance = PushSubscriptionSingleton.Instance;
        private static int _lastId;

        public Task SaveAsync(PushSubscription subscription, CancellationToken cancellationToken)
        {
            var stored = _instance.FirstOrDefault(existing => existing.Endpoint == subscription.Endpoint);
            var sameOwner = stored is not null && stored.UserId == subscription.UserId;

            if (stored is not null)
                _instance.Remove(stored);

            var saved = PushSubscription.FromPersistence(
                stored?.Id!.Value ?? Interlocked.Increment(ref _lastId),
                subscription.UserId.Value,
                subscription.Endpoint,
                subscription.P256dh,
                subscription.Auth,
                subscription.Label,
                sameOwner ? stored!.CreatedAt : subscription.CreatedAt,
                sameOwner ? stored!.LastSuccessAt : null);
            _instance.Add(saved);

            var dropped = _instance
                .Where(existing => existing.UserId == subscription.UserId)
                .OrderByDescending(existing => existing.Endpoint == subscription.Endpoint)
                .ThenByDescending(existing => existing.LastSuccessAt ?? existing.CreatedAt)
                .ThenByDescending(existing => existing.Id!.Value)
                .Skip(PushSubscription.MaxPerUser)
                .ToList();
            _instance.RemoveAll(dropped.Contains);

            return Task.CompletedTask;
        }

        public Task DeleteAsync(UserId userId, string endpoint, CancellationToken cancellationToken)
        {
            _instance.RemoveAll(existing => existing.UserId == userId && existing.Endpoint == endpoint);
            return Task.CompletedTask;
        }
    }
}
