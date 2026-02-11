using LifeManager.Domain.Exceptions;

namespace LifeManager.Domain.Users
{
    public class UserPassword
    {
        public string Value { get; }

        private UserPassword(string value)
        {
            Value = value;
        }

        public static UserPassword Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException($"{nameof(UserPassword)} is required");

            const short MaxLength = 50;
            if (value.Length > MaxLength)
                throw new DomainException($"{nameof(UserPassword)} cannot be longer than {MaxLength} characters");

            const short MinLength = 8;
            if (value.Length < MinLength)
                throw new DomainException($"{nameof(UserPassword)} must be at least {MinLength} characters long");

            return new UserPassword(value);
        }

        public override bool Equals(object? obj)
        {
            if (obj is UserPassword other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}