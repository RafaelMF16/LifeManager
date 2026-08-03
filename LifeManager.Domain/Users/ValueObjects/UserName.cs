using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.Errors;

namespace LifeManager.Domain.Users.ValueObjects
{
    public class UserName
    {
        public string Value { get; }

        private UserName(string value)
        {
            Value = value;
        }

        public static Result<UserName> Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return UserErrors.UserNameIsNullOrWhiteSpace;
            
            const short MaxLength = 100;
            if (value.Length > MaxLength)
                return UserErrors.UserNameTooLong;

            return new UserName(value);
        }

        public override bool Equals(object? obj)
        {
            if (obj is UserName other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}