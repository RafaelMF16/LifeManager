namespace LifeManager.Application.Auth.DTOs
{
    /// <summary>The token from an auth e-mail link. Always sent in the body, never in the query string.</summary>
    public record EmailTokenDto(string Token);
}
