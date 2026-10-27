using LifeManager.Domain.Auth.Enums;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Auth.Interfaces
{
    public interface IEmailCooldownRepository
    {
        /// <summary>
        /// Reserves the next e-mail of <paramref name="kind"/> to <paramref name="email"/>: true (and the send time moves to
        /// <paramref name="now"/>) when none went out in the last <see cref="EmailCooldown.Duration"/>, false otherwise.
        /// Atomic, so two concurrent requests can't both get true.
        /// </summary>
        Task<bool> TryReserveAsync(Email email, EmailKind kind, DateTimeOffset now, CancellationToken cancellationToken);

        Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
    }
}
