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

        /// <summary>The current password typed in "My account" is wrong. A 400, not a 401: the frontend reads every 401 outside <c>/api/Auth</c> as an expired session.</summary>
        public static readonly Error WrongPassword = Error.Validation("User.WrongPassword", "The password is wrong");

        public static readonly Error InvalidCredentials = Error.Unauthorized("User.InvalidCredentials", "Invalid email or password");
    }
}