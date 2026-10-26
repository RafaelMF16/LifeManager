using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Enums;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Auth.Mocks
{
    /// <summary>Mirrors the conditional upsert: insert when missing, move the send time only when the cooldown passed.</summary>
    public class EmailCooldownRepositoryMock : IEmailCooldownRepository
    {
        private readonly EmailCooldownSingleton _instance = EmailCooldownSingleton.Instance;

        public Task<bool> TryReserveAsync(Email email, EmailKind kind, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var key = EmailCooldown.KeyOf(email.Value);
            var cooldown = _instance.Find(row => row.Email == key && row.Kind == kind);

            if (cooldown is null)
            {
                _instance.Add(EmailCooldown.Create(key, kind, now));
                return Task.FromResult(true);
            }

            if (!cooldown.CanSendAgain(now))
                return Task.FromResult(false);

            cooldown.MarkSent(now);
            return Task.FromResult(true);
        }

        public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
            => Task.FromResult(_instance.RemoveAll(row => row.LastSentAt < cutoff));
    }
}
