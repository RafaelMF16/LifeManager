using LifeManager.Domain.Push.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Push.Interfaces
{
    public interface IPushSubscriptionRepository
    {
        /// <summary>
        /// Stores the device by its endpoint: a new one is added, a known one gets the new keys and label and, when it
        /// belonged to another user, changes owner. Then keeps only the user's <see cref="PushSubscription.MaxPerUser"/>
        /// most recently used devices.
        /// </summary>
        Task SaveAsync(PushSubscription subscription, CancellationToken cancellationToken);

        /// <summary>Deletes the user's device with that endpoint; nothing when there is none (or it is someone else's).</summary>
        Task DeleteAsync(UserId userId, string endpoint, CancellationToken cancellationToken);

        Task<IReadOnlyList<PushSubscription>> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken);

        /// <summary>Deletes a dead device, only while it is still the user's (another account may have taken it over).</summary>
        Task DeleteByIdAsync(PushSubscriptionId id, UserId userId, CancellationToken cancellationToken);

        /// <summary>Records that the push service accepted a notification for the device.</summary>
        Task MarkSuccessAsync(PushSubscriptionId id, DateTimeOffset at, CancellationToken cancellationToken);
    }
}
