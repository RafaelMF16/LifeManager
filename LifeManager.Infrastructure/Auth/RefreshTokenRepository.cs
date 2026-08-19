using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;
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

        public RefreshToken? GetValidTokenByTokenHash(string hashedRefreshToken)
        {
            throw new NotImplementedException();
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
    }
}