using LifeManager.Domain.Exceptions;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.Errors;

namespace LifeManager.Domain.Users.ValueObjects
{
    public class PlainPassword
    {
        public string Value { get; }

        private PlainPassword(string value)
        {
            Value = value;
        }

        public static Result<PlainPassword> Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return UserErrors.PlainPasswordIsNullOrWhiteSpace;

            const short MaxLength = 50;
            if (value.Length > MaxLength)
                return UserErrors.PlainPasswordTooLong;

            const short MinLength = 8;
            if (value.Length < MinLength)
                return UserErrors.PlainPasswordTooShort;

            return new PlainPassword(value);
        }

        public override bool Equals(object? obj)
        {
            if (obj is PlainPassword other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}