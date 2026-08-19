namespace LifeManager.Domain.Auth.Interfaces
{
    public interface IRefreshTokenRepository
    {
        RefreshToken Add(RefreshToken refreshToken);
        RefreshToken? GetValidTokenByTokenHash(string hashedRefreshToken);
        RefreshToken ReplaceActiveToken(RefreshToken newToken);
    }
}