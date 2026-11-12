namespace LifeManager.Application.Push.DTOs
{
    /// <summary>Body of <c>PUT /api/Push/Subscriptions</c>: the browser's <c>PushSubscription.toJSON()</c> plus a label.</summary>
    /// <param name="Label">What the user sees for the device, e.g. "Chrome · Android"; optional.</param>
    public sealed record PushSubscriptionDto(string? Endpoint, PushSubscriptionKeysDto? Keys, string? Label = null);

    public sealed record PushSubscriptionKeysDto(string? P256dh, string? Auth);

    /// <summary>Body of <c>DELETE /api/Push/Subscriptions</c>.</summary>
    public sealed record PushSubscriptionDeleteDto(string? Endpoint);
}
