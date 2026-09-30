using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Auth
{
    public class RefreshTokenRepository(LifeManagerDbContext dbContext) : IRefreshTokenRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public RefreshToken Add(RefreshToken refreshToken)
        {
            _dbContext.Add(refreshToken);
            _dbContext.SaveChanges();

            return refreshToken;
        }

        public RefreshToken ReplaceActiveToken(RefreshToken newToken)
        {
            var activeToken = _dbContext.RefreshTokens
                .SingleOrDefault(refreshToken => refreshToken.UserId == newToken.UserId && !refreshToken.IsRevoked && refreshToken.ExpiresAt > DateTimeOffset.UtcNow);

            activeToken?.RevokeToken();

            _dbContext.Add(newToken);
            _dbContext.SaveChanges();

            return newToken;
        }

        public async Task<bool> RevokeByHashAsync(RefreshTokenHash tokenHash, CancellationToken cancellationToken)
        {
            var revokedRows = await _dbContext.RefreshTokens
                .Where(refreshToken => refreshToken.TokenHash == tokenHash && !refreshToken.IsRevoked)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(refreshToken => refreshToken.IsRevoked, true), cancellationToken);

            return revokedRows > 0;
        }
    }
}