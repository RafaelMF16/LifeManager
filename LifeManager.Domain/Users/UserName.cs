using LifeManager.Domain.Exceptions;

namespace LifeManager.Domain.Users
{
    public class UserName
    {
        public string Value { get; }

        private UserName(string value)
        {
            Value = value;
        }

        public static UserName Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException($"{nameof(UserName)} is required");
            
            const short MaxLength = 100;
            if (value.Length > MaxLength)
                throw new DomainException($"{nameof(UserName)} cannot be longer than {MaxLength} characters.");

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