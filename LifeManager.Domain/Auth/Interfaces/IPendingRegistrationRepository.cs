using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Auth.Interfaces
{
    public interface IPendingRegistrationRepository
    {
        Task<PendingRegistration?> GetByEmailAsync(Email email, CancellationToken cancellationToken);

        Task<PendingRegistration?> GetByTokenHashAsync(EmailTokenHash tokenHash, CancellationToken cancellationToken);

        /// <summary>
        /// Stores the sign-up by e-mail: inserts it, or (another request got there first, or it's a renewal) replaces the
        /// stored one's name, preferences, token and validity, so the e-mail never has two pending sign-ups.
        /// </summary>
        Task SaveAsync(PendingRegistration pendingRegistration, CancellationToken cancellationToken);

        /// <summary>
        /// Turns the sign-up into an account, in one database transaction: deletes it (only while it still has this
        /// token, so a link can't be used twice and a renewed sign-up's old link fails), then adds <paramref name="user"/>
        /// with their preferences and default categories in the sign-up's language and theme. Null when the sign-up was
        /// already used or renewed, or the e-mail got an account meanwhile; nothing is written then.
        /// </summary>
        Task<User?> CompleteAsync(PendingRegistration pendingRegistration, User user, CancellationToken cancellationToken);

        Task<int> DeleteExpiredBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
    }
}
