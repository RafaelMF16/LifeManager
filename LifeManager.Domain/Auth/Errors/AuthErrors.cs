using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Auth.Errors
{
    public static class AuthErrors
    {
        public static readonly Error RefreshTokenHashIsNullOrWhiteSpace = Error.Validation("Auth.RefreshTokenHashIsNullOrWhiteSpace", "RefreshTokenHash is required");

        // Single error for missing, unknown, expired, revoked or reused tokens, so the API never reveals whether a token exists.
        public static readonly Error InvalidRefreshToken = Error.Unauthorized("Auth.InvalidRefreshToken", "Refresh token is invalid or expired");

        // Single error for an unknown, expired or used e-mail token (sign-up or password reset), like the refresh token.
        public static readonly Error InvalidEmailToken = Error.Validation("Auth.InvalidEmailToken", "The link is invalid or has expired");

        // Written by the rate limiter's rejection, not returned by a service.
        public static readonly Error TooManyRequests = Error.Failure("Auth.TooManyRequests", "Too many requests, try again later");

        // Never reaches the client: the auth flows answer the same whether or not the e-mail went out.
        public static readonly Error EmailNotSent = Error.Failure("Auth.EmailNotSent", "The e-mail could not be sent");
    }
}