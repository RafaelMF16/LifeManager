using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;

namespace LifeManager.Application.Test.Auth.Mocks
{
    public class RefreshTokenRepositoryMock : IRefreshTokenRepository
    {
        public RefreshToken Add(RefreshToken refreshToken)
        {
            throw new NotImplementedException();
        }

        public RefreshToken? GetValidTokenByTokenHash(string hashedRefreshToken)
        {
            throw new NotImplementedException();
        }

        public RefreshToken? GetValidTokenByUserId(int userId)
        {
            throw new NotImplementedException();
        }

        public RefreshToken UpdateRevoked(RefreshToken refreshToken)
        {
            throw new NotImplementedException();
        }
    }
}