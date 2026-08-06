namespace LifeManager.Domain.Auth.Interfaces
{
    public interface IRefreshTokenRepository
    {
        RefreshToken Add(RefreshToken refreshToken);
        RefreshToken? GetValidTokenByTokenHash(string hashedRefreshToken);
        RefreshToken? GetValidTokenByUserId(int userId);
        RefreshToken UpdateRevoked(RefreshToken refreshToken);
    }
}