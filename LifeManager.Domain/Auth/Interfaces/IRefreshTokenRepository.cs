using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Auth.Interfaces
{
    public interface IRefreshTokenRepository
    {
        /// <summary>
        /// Revokes every token of the user still active at <paramref name="now"/> and adds the new one in a single save.
        /// <paramref name="now"/> is the caller's clock, the same instant that stamped the new token.
        /// </summary>
        Task ReplaceActiveTokenAsync(RefreshToken newToken, DateTimeOffset now, CancellationToken cancellationToken);

        /// <returns>True if a non-revoked token with this hash existed and was revoked.</returns>
        Task<bool> RevokeByHashAsync(RefreshTokenHash tokenHash, CancellationToken cancellationToken);

        Task<RefreshToken?> GetByHashAsync(RefreshTokenHash tokenHash, CancellationToken cancellationToken);

        /// <summary>Atomically revokes the token only if it is still active (not revoked and not expired).</summary>
        /// <returns>True if this call revoked it; false if it was already consumed, e.g. by a concurrent refresh.</returns>
        Task<bool> TryConsumeAsync(RefreshTokenHash tokenHash, DateTimeOffset now, CancellationToken cancellationToken);

        Task RevokeAllActiveByUserIdAsync(UserId userId, CancellationToken cancellationToken);

        Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken);
    }
}
