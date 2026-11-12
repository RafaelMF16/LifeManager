using LifeManager.Application.Push.DTOs;
using LifeManager.Domain.Push.Errors;
using LifeManager.Domain.Push.Interfaces;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Application.Push.Services
{
    /// <summary>The browser side of Web Push: the VAPID public key a device subscribes with.</summary>
    public class PushService(IPushSender pushSender)
    {
        private readonly IPushSender _pushSender = pushSender;

        public Result<PushPublicKeyDto> GetPublicKey()
            => _pushSender.PublicKey is { } publicKey
                ? new PushPublicKeyDto(publicKey)
                : PushErrors.NotConfigured;
    }
}
