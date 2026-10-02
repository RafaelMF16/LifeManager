using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Auth.Mocks
{
    public class RefreshTokenRepositoryMock : IRefreshTokenRepository
    {
        private readonly RefreshTokenSingleton _instance;

        public RefreshTokenRepositoryMock()
        {
            _instance = RefreshTokenSingleton.Instance;
        }

        public Task ReplaceActiveTokenAsync(RefreshToken newToken, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var activeToken in _instance.Where(token => token.UserId == newToken.UserId && !token.IsRevoked && token.ExpiresAt > now))
                activeToken.RevokeToken();

            _instance.Add(newToken);

            return Task.CompletedTask;
        }

        public Task<bool> RevokeByHashAsync(RefreshTokenHash tokenHash, CancellationToken cancellationToken)
        {
            var activeToken = _instance.Find(token => token.TokenHash.Equals(tokenHash) && !token.IsRevoked);
            activeToken?.RevokeToken();

            return Task.FromResult(activeToken is not null);
        }

        public Task<RefreshToken?> GetByHashAsync(RefreshTokenHash tokenHash, CancellationToken cancellationToken)
            => Task.FromResult(_instance.Find(token => token.TokenHash.Equals(tokenHash)));

        public Task<bool> TryConsumeAsync(RefreshTokenHash tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var activeToken = _instance.Find(token => token.TokenHash.Equals(tokenHash) && !token.IsRevoked && token.ExpiresAt > now);
            activeToken?.RevokeToken();

            return Task.FromResult(activeToken is not null);
        }

        public Task RevokeAllActiveByUserIdAsync(UserId userId, CancellationToken cancellationToken)
        {
            foreach (var token in _instance.Where(token => token.UserId.Equals(userId) && !token.IsRevoked))
                token.RevokeToken();

            return Task.CompletedTask;
        }

        public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
        {
            _instance.Add(refreshToken);
            return Task.CompletedTask;
        }
    }
}
