using LifeManager.Application.Push.DTOs;
using LifeManager.Domain.Push;
using LifeManager.Domain.Push.Errors;
using LifeManager.Domain.Push.Interfaces;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Push.Services
{
    /// <summary>The browser side of Web Push: the VAPID public key a device subscribes with, and the user's devices.</summary>
    public class PushService(IPushSender pushSender, IPushSubscriptionRepository pushSubscriptionRepository, TimeProvider timeProvider)
    {
        private readonly IPushSender _pushSender = pushSender;
        private readonly IPushSubscriptionRepository _pushSubscriptionRepository = pushSubscriptionRepository;
        private readonly TimeProvider _timeProvider = timeProvider;

        public Result<PushPublicKeyDto> GetPublicKey()
            => _pushSender.PublicKey is { } publicKey
                ? new PushPublicKeyDto(publicKey)
                : PushErrors.NotConfigured;

        public async Task<Result> SaveSubscriptionAsync(PushSubscriptionDto dto, UserId userId, CancellationToken cancellationToken)
        {
            var subscriptionResult = PushSubscription.Create(
                userId.Value, dto.Endpoint, dto.Keys?.P256dh, dto.Keys?.Auth, dto.Label, _timeProvider.GetUtcNow());
            if (!subscriptionResult.IsSuccess)
                return subscriptionResult.Error;

            await _pushSubscriptionRepository.SaveAsync(subscriptionResult.Value, cancellationToken);

            return Result.Success();
        }

        /// <summary>Idempotent: an unknown, empty or someone else's endpoint changes nothing and still succeeds.</summary>
        public async Task DeleteSubscriptionAsync(PushSubscriptionDeleteDto dto, UserId userId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dto.Endpoint))
                return;

            await _pushSubscriptionRepository.DeleteAsync(userId, dto.Endpoint, cancellationToken);
        }
    }
}
