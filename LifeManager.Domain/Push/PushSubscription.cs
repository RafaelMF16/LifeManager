using LifeManager.Domain.Push.Errors;
using LifeManager.Domain.Push.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;
using System.Security.Cryptography;

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

        /// <summary>
        /// Devices kept per user: subscribing one more drops the one used longest ago, so browsers reinstalled or wiped
        /// over time don't pile up.
        /// </summary>
        public const int MaxPerUser = 10;

        private const int P256dhLength = 65;
        private const int AuthLength = 16;

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

        /// <param name="endpoint">Must be <c>https</c> on a known push service (<see cref="PushServiceHosts"/>): the server posts to it.</param>
        /// <param name="p256dh">The browser's P-256 public key (65 bytes), base64url.</param>
        /// <param name="auth">The browser's auth secret (16 bytes), base64url.</param>
        public static Result<PushSubscription> Create(
            int idUser,
            string? endpoint,
            string? p256dh,
            string? auth,
            string? label,
            DateTimeOffset now)
        {
            if (!PushServiceHosts.IsAllowed(endpoint))
                return PushErrors.InvalidEndpoint;

            if (!TryDecode(p256dh, P256dhLength, out var publicKey) || !IsP256Point(publicKey) || !TryDecode(auth, AuthLength, out _))
                return PushErrors.InvalidKeys;

            var trimmedLabel = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
            if (trimmedLabel?.Length > LabelMaxLength)
                return PushErrors.LabelTooLong;

            return new PushSubscription(new UserId(idUser), endpoint!, p256dh!, auth!, trimmedLabel, now, lastSuccessAt: null);
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

        /// <summary>Decodes base64url (padding allowed) of exactly <paramref name="length"/> bytes.</summary>
        private static bool TryDecode(string? value, int length, out byte[] bytes)
        {
            bytes = [];
            if (string.IsNullOrWhiteSpace(value) || value.Length > KeyMaxLength)
                return false;

            var base64 = value.TrimEnd('=').Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');

            var buffer = new byte[base64.Length];
            if (!Convert.TryFromBase64String(base64, buffer, out var written) || written != length)
                return false;

            bytes = buffer[..written];
            return true;
        }

        /// <summary>
        /// Whether the key is an uncompressed point on P-256 (0x04, X, Y): the payload is encrypted for it, and a key
        /// that isn't one would fail on every send.
        /// </summary>
        private static bool IsP256Point(byte[] key)
        {
            if (key[0] != 0x04)
                return false;

            try
            {
                using var ecdh = ECDiffieHellman.Create(new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint { X = key[1..33], Y = key[33..] },
                });
                return true;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }
    }
}
