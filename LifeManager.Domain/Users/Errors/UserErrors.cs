using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Users.Errors
{
    public static class UserErrors
    {
        public static readonly Error NotFound = Error.NotFound("User.NotFound", "User not found");

        public static readonly Error UserNameIsNullOrWhiteSpace = Error.Validation("User.UserNameIsNullOrWhiteSpace", "UserName is required");
        public static readonly Error UserNameTooLong = Error.Validation("User.UserNameTooLong", "UserName cannot be longer than 100 characters");
        public static readonly Error EmailIsNullOrWhiteSpace = Error.Validation("User.EmailIsNullOrWhiteSpace", "Email is required");
        public static readonly Error EmailIsInvalid = Error.Validation("User.EmailIsInvalid", "Email is invalid");
        public static readonly Error PasswordHashIsNullOrWhiteSpace = Error.Validation("User.PasswordHashIsNullOrWhiteSpace", "PasswordHash is required");
        public static readonly Error PlainPasswordIsNullOrWhiteSpace = Error.Validation("User.PlainPasswordIsNullOrWhiteSpace", "PlainPassword is required");
        public static readonly Error PlainPasswordTooLong = Error.Validation("User.PlainPasswordTooLong", "PlainPassword cannot be longer than 50 characters");
        public static readonly Error PlainPasswordTooShort = Error.Validation("User.PlainPasswordTooShort", "PlainPassword must be at least 8 characters long");

        public static readonly Error EmailRegistered = Error.Conflict("User.EmailRegistered", "Email already registered");
    }
}