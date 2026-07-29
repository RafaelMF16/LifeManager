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
            return _instance.FirstOrDefault(token => token.TokenHash.Value == hashedRefreshToken && !token.IsRevoked.Value);
        }

        public RefreshToken? GetValidTokenByUserId(int userId)
        {
            return _instance.FirstOrDefault(token => token.UserId.Value == userId && !token.IsRevoked.Value);
        }

        public RefreshToken UpdateRevoked(RefreshToken refreshToken)
        {
            return refreshToken;
        }
    }
}
