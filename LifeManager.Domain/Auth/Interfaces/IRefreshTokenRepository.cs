using LifeManager.Domain.Auth.ValueObjects;

namespace LifeManager.Domain.Auth.Interfaces
{
    public interface IRefreshTokenRepository
    {
        RefreshToken Add(RefreshToken refreshToken);
        RefreshToken ReplaceActiveToken(RefreshToken newToken);

        /// <returns>True if a non-revoked token with this hash existed and was revoked.</returns>
        Task<bool> RevokeByHashAsync(RefreshTokenHash tokenHash, CancellationToken cancellationToken);
    }
}