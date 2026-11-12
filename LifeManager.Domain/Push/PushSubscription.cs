using LifeManager.Domain.Push.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Push
{
    /// <summary>
    /// One browser (device) that receives the user's notifications: the push service's endpoint and the two keys the
    /// payload is encrypted with. The endpoint is unique across users: the same device signed in to another account
    /// changes owner instead of notifying both.
    /// </summary>
    public class PushSubscription
    {
        public const int EndpointMaxLength = 1000;
        public const int KeyMaxLength = 100;
        public const int LabelMaxLength = 60;

        public PushSubscriptionId? Id { get; private set; }
        public UserId UserId { get; private set; }
        public string Endpoint { get; private set; }
        public string P256dh { get; private set; }
        public string Auth { get; private set; }

        /// <summary>What the user sees in the device list, e.g. "Chrome · Android"; null without one.</summary>
        public string? Label { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        /// <summary>The last notification the push service accepted; null before the first.</summary>
        public DateTimeOffset? LastSuccessAt { get; private set; }

        private PushSubscription(
            UserId userId,
            string endpoint,
            string p256dh,
            string auth,
            string? label,
            DateTimeOffset createdAt,
            DateTimeOffset? lastSuccessAt)
        {
            UserId = userId;
            Endpoint = endpoint;
            P256dh = p256dh;
            Auth = auth;
            Label = label;
            CreatedAt = createdAt;
            LastSuccessAt = lastSuccessAt;
        }

        internal static PushSubscription FromPersistence(
            int id,
            int idUser,
            string endpoint,
            string p256dh,
            string auth,
            string? label,
            DateTimeOffset createdAt,
            DateTimeOffset? lastSuccessAt)
        {
            var subscription = new PushSubscription(new UserId(idUser), endpoint, p256dh, auth, label, createdAt, lastSuccessAt);
            subscription.AssignId(id);
            return subscription;
        }

        public void AssignId(int id)
        {
            Id = new PushSubscriptionId(id);
        }
    }
}
