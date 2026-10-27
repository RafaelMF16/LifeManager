using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Auth.Interfaces
{
    public interface IPasswordResetTokenRepository
    {
        /// <summary>Deletes the user's links not used yet and adds the new one, in one database transaction.</summary>
        Task AddReplacingUnusedAsync(PasswordResetToken token, CancellationToken cancellationToken);

        Task<PasswordResetToken?> GetByTokenHashAsync(EmailTokenHash tokenHash, CancellationToken cancellationToken);

        /// <summary>
        /// Uses the link, sets the user's new password and revokes every active refresh token of the user, in one database
        /// transaction, so the password never changes while the old sessions stay open. The link is marked used only while
        /// it is unused and unexpired (a conditional update), so two concurrent resets can't both win; false (and nothing
        /// written) for the loser or a link that can't be used anymore.
        /// </summary>
        Task<bool> UseAsync(PasswordResetToken token, PasswordHash newPasswordHash, DateTimeOffset now, CancellationToken cancellationToken);

        /// <summary>Deletes links that expired, or were used, before <paramref name="cutoff"/>.</summary>
        Task<int> DeleteStaleBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
    }
}
