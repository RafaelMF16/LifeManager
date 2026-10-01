using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Auth.Errors
{
    public static class AuthErrors
    {
        public static readonly Error RefreshTokenHashIsNullOrWhiteSpace = Error.Validation("Auth.RefreshTokenHashIsNullOrWhiteSpace", "RefreshTokenHash is required");

        // Single error for missing, unknown, expired, revoked or reused tokens, so the API never reveals whether a token exists.
        public static readonly Error InvalidRefreshToken = Error.Unauthorized("Auth.InvalidRefreshToken", "Refresh token is invalid or expired");
    }
}