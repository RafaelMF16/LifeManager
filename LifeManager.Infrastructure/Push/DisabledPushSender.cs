using LifeManager.Domain.Push.Interfaces;

namespace LifeManager.Infrastructure.Push
{
    /// <summary>
    /// Development without VAPID keys: push is off (<see cref="PublicKey"/> null, so the frontend can't subscribe and the
    /// reminders job has nothing to do). Sending is a bug, so it throws.
    /// </summary>
    public sealed class DisabledPushSender : IPushSender
    {
        public string? PublicKey => null;

        public Task<PushSendResult> SendAsync(PushTarget target, PushPayload payload, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Push notifications are disabled (no VAPID keys)");
    }
}
