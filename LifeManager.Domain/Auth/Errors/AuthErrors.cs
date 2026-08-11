using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Auth.Errors
{
    public static class AuthErrors
    {
        public static readonly Error RefreshTokenHashIsNullOrWhiteSpace = Error.Validation("Auth.RefreshTokenHashIsNullOrWhiteSpace", "RefreshTokenHash is required");
    }
}