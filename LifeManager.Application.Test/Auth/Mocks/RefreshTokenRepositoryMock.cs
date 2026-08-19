using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;

namespace LifeManager.Application.Test.Auth.Mocks
{
    public class RefreshTokenRepositoryMock : IRefreshTokenRepository
    {
        private readonly RefreshTokenSingleton _instance;

        public RefreshTokenRepositoryMock()
        {
            _instance = RefreshTokenSingleton.Instance;
        }

        public RefreshToken Add(RefreshToken refreshToken)
        {
            _instance.Add(refreshToken);
            return refreshToken;
        }

        public RefreshToken? GetValidTokenByTokenHash(string hashedRefreshToken)
        {
            return _instance.Find(token => token.TokenHash.Value == hashedRefreshToken && !token.IsRevoked && token.ExpiresAt > DateTimeOffset.UtcNow);
        }

        public RefreshToken ReplaceActiveToken(RefreshToken newToken)
        {
            var activeToken = _instance.Find(token => token.UserId == newToken.UserId && !token.IsRevoked && token.ExpiresAt > DateTimeOffset.UtcNow);
            activeToken?.RevokeToken();

            _instance.Add(newToken);

            return newToken;
        }
    }
}
