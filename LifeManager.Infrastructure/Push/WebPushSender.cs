using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using LifeManager.Domain.Push.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace LifeManager.Infrastructure.Push
{
    public sealed record VapidSettings(string PublicKey, string PrivateKey, string Subject);

    /// <summary>
    /// Web Push with VAPID through <c>Lib.Net.Http.WebPush</c>: <c>aes128gcm</c> encryption (RFC 8291) and the
    /// <c>vapid t=…, k=…</c> scheme (RFC 8292), which Apple's push service requires besides Chrome's and Firefox's. The
    /// <see cref="HttpClient"/> comes from <c>AddHttpClient</c> in the WebApi with the timeout set. 404/410 mean the
    /// subscription is dead (<see cref="PushSendResult.Gone"/>); any other failure is logged and reported as
    /// <see cref="PushSendResult.Failed"/>, never thrown, so one device never stops the others.
    /// </summary>
    public sealed class WebPushSender : IPushSender, IDisposable
    {
        /// <summary>
        /// How long the push service keeps a notification for an offline device. A reminder an hour late is useless, so
        /// it's short (the push services' default is weeks).
        /// </summary>
        public const int TimeToLiveSeconds = 60 * 60;

        /// <summary>How much of a push service's error body goes to the log: enough for its reason, never an unbounded third-party text.</summary>
        public const int MaxLoggedBodyLength = 200;

        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        private readonly PushServiceClient _client;
        private readonly VapidAuthentication _authentication;
        private readonly ILogger<WebPushSender> _logger;

        public WebPushSender(HttpClient httpClient, VapidSettings settings, ILogger<WebPushSender> logger)
        {
            _authentication = new VapidAuthentication(settings.PublicKey, settings.PrivateKey) { Subject = settings.Subject };
            _client = new PushServiceClient(httpClient)
            {
                DefaultAuthentication = _authentication,
                DefaultAuthenticationScheme = VapidAuthenticationScheme.Vapid,
                DefaultTimeToLive = TimeToLiveSeconds,
                // A 429 is reported as a failure instead of holding the hourly job for the Retry-After.
                AutoRetryAfter = false,
            };
            _logger = logger;
            PublicKey = settings.PublicKey;
        }

        public string? PublicKey { get; }

        public async Task<PushSendResult> SendAsync(PushTarget target, PushPayload payload, CancellationToken cancellationToken)
        {
            var subscription = new PushSubscription { Endpoint = target.Endpoint };
            subscription.SetKey(PushEncryptionKeyName.P256DH, target.P256dh);
            subscription.SetKey(PushEncryptionKeyName.Auth, target.Auth);

            var message = new PushMessage(JsonSerializer.Serialize(payload, SerializerOptions))
            {
                TimeToLive = TimeToLiveSeconds,
            };

            try
            {
                await _client.RequestPushMessageDeliveryAsync(subscription, message, cancellationToken);
                return PushSendResult.Sent;
            }
            catch (PushServiceClientException exception) when (exception.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                return PushSendResult.Gone;
            }
            catch (PushServiceClientException exception)
            {
                _logger.LogError("The push service at {Host} rejected a notification: {StatusCode} {Body}", HostOf(target), (int)exception.StatusCode, Clip(exception.Body));
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested
                && exception is HttpRequestException or TaskCanceledException or CryptographicException or ArgumentException or FormatException or InvalidOperationException)
            {
                // Cryptographic/Argument/Format/InvalidOperation: keys the library can't encrypt with. Failed, never Gone: it
                // may be our VAPID key, and Gone would delete every device. Subscribing already refuses a key off the curve.
                _logger.LogError(exception, "Could not send a notification through the push service at {Host}", HostOf(target));
            }

            return PushSendResult.Failed;
        }

        public void Dispose()
            => _authentication.Dispose();

        private static string Clip(string? body)
            => body is null || body.Length <= MaxLoggedBodyLength ? body ?? "" : body[..MaxLoggedBodyLength] + "…";

        // The endpoint's path identifies the device, so only the host goes to the log.
        private static string HostOf(PushTarget target)
            => Uri.TryCreate(target.Endpoint, UriKind.Absolute, out var uri) ? uri.Host : "?";
    }
}
