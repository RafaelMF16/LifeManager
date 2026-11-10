namespace LifeManager.Application.Auth.DTOs
{
    /// <param name="DeletionCancelled">The sign-in cancelled a pending account deletion, so the frontend tells the user.</param>
    public record LoginResponseDto(string AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, bool DeletionCancelled = false);
}
