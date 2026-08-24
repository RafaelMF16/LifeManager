namespace LifeManager.Domain.Auth.Interfaces
{
    public interface IRefreshTokenRepository
    {
        RefreshToken Add(RefreshToken refreshToken);
        RefreshToken ReplaceActiveToken(RefreshToken newToken);
    }
}