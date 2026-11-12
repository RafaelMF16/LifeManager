namespace LifeManager.Domain.Push.Interfaces
{
    /// <summary>Where a notification goes: a browser's push subscription (endpoint and its two base64url keys).</summary>
    public sealed record PushTarget(string Endpoint, string P256dh, string Auth);

    /// <summary>What the frontend's service worker shows: title, body, the page a tap opens and the tag that groups them.</summary>
    public sealed record PushPayload(string Title, string Body, string Url, string Tag);

    public enum PushSendResult
    {
        Sent = 1,

        /// <summary>The push service answered 404/410: the subscription is dead and should be deleted.</summary>
        Gone = 2,

        /// <summary>Anything else (logged by the sender): kept, tried again on the next reminder.</summary>
        Failed = 3,
    }

    /// <summary>
    /// Sends Web Push notifications (VAPID). Implemented in Infrastructure; without VAPID keys (Development only) it's
    /// disabled: <see cref="PublicKey"/> is null and nothing should be sent.
    /// </summary>
    public interface IPushSender
    {
        /// <summary>The VAPID public key the browser subscribes with; null when push isn't configured.</summary>
        string? PublicKey { get; }

        Task<PushSendResult> SendAsync(PushTarget target, PushPayload payload, CancellationToken cancellationToken);
    }
}
