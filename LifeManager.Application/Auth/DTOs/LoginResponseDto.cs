namespace LifeManager.Application.Auth.DTOs
{
    public record LoginResponseDto(string AccessToken, string RefreshToken);
}
