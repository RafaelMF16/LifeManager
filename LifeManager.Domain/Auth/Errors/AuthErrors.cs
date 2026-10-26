using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Auth.Errors
{
    public static class AuthErrors
    {
        public static readonly Error RefreshTokenHashIsNullOrWhiteSpace = Error.Validation("Auth.RefreshTokenHashIsNullOrWhiteSpace", "RefreshTokenHash is required");

        // Single error for missing, unknown, expired, revoked or reused tokens, so the API never reveals whether a token exists.
        public static readonly Error InvalidRefreshToken = Error.Unauthorized("Auth.InvalidRefreshToken", "Refresh token is invalid or expired");

        // Never reaches the client: the auth flows answer the same whether or not the e-mail went out.
        public static readonly Error EmailNotSent = Error.Failure("Auth.EmailNotSent", "The e-mail could not be sent");
    }
}