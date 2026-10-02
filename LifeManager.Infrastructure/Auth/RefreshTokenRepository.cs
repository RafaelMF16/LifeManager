using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Auth
{
    public class RefreshTokenRepository(LifeManagerDbContext dbContext) : IRefreshTokenRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task ReplaceActiveTokenAsync(RefreshToken newToken, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var activeTokens = await _dbContext.RefreshTokens
                .Where(refreshToken => refreshToken.UserId == newToken.UserId && !refreshToken.IsRevoked && refreshToken.ExpiresAt > now)
                .ToListAsync(cancellationToken);

            foreach (var activeToken in activeTokens)
                activeToken.RevokeToken();

            _dbContext.Add(newToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> RevokeByHashAsync(RefreshTokenHash tokenHash, CancellationToken cancellationToken)
        {
            var revokedRows = await _dbContext.RefreshTokens
                .Where(refreshToken => refreshToken.TokenHash == tokenHash && !refreshToken.IsRevoked)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(refreshToken => refreshToken.IsRevoked, true), cancellationToken);

            return revokedRows > 0;
        }

        public async Task<RefreshToken?> GetByHashAsync(RefreshTokenHash tokenHash, CancellationToken cancellationToken)
        {
            return await _dbContext.RefreshTokens
                .AsNoTracking()
                .SingleOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);
        }

        public async Task<bool> TryConsumeAsync(RefreshTokenHash tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var consumedRows = await _dbContext.RefreshTokens
                .Where(refreshToken => refreshToken.TokenHash == tokenHash && !refreshToken.IsRevoked && refreshToken.ExpiresAt > now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(refreshToken => refreshToken.IsRevoked, true), cancellationToken);

            return consumedRows > 0;
        }

        public async Task RevokeAllActiveByUserIdAsync(UserId userId, CancellationToken cancellationToken)
        {
            await _dbContext.RefreshTokens
                .Where(refreshToken => refreshToken.UserId == userId && !refreshToken.IsRevoked)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(refreshToken => refreshToken.IsRevoked, true), cancellationToken);
        }

        public async Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
        {
            _dbContext.Add(refreshToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}