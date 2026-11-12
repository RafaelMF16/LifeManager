using LifeManager.Domain.Push.Interfaces;

namespace LifeManager.Application.Test.Push.Mocks
{
    /// <summary>
    /// Keeps the sent notifications in <see cref="Sent"/> (shared, like <c>EmailSenderMock</c>) instead of sending them.
    /// <see cref="ResultFor"/> answers per endpoint (default <see cref="PushSendResult.Sent"/>); <see cref="Enabled"/>
    /// = false stands for a host without VAPID keys.
    /// </summary>
    public class PushSenderMock : IPushSender
    {
        public const string TestPublicKey = "BTestPublicKey";

        public static List<(PushTarget Target, PushPayload Payload)> Sent { get; } = [];

        public static Dictionary<string, PushSendResult> ResultFor { get; } = [];

        public static bool Enabled { get; set; } = true;

        public static void Reset()
        {
            Sent.Clear();
            ResultFor.Clear();
            Enabled = true;
        }

        public string? PublicKey => Enabled ? TestPublicKey : null;

        public Task<PushSendResult> SendAsync(PushTarget target, PushPayload payload, CancellationToken cancellationToken)
        {
            if (!Enabled)
                throw new InvalidOperationException("Push is disabled: nothing should be sent");

            var result = ResultFor.GetValueOrDefault(target.Endpoint, PushSendResult.Sent);
            if (result == PushSendResult.Sent)
                Sent.Add((target, payload));

            return Task.FromResult(result);
        }
    }
}
