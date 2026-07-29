using LifeManager.Domain.Exceptions;

namespace LifeManager.Domain.Users.ValueObjects
{
    public class PlainPassword
    {
        public string Value { get; }

        private PlainPassword(string value)
        {
            Value = value;
        }

        public static PlainPassword Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException($"{nameof(PlainPassword)} is required");

            const short MaxLength = 50;
            if (value.Length > MaxLength)
                throw new DomainException($"{nameof(PlainPassword)} cannot be longer than {MaxLength} characters");

            const short MinLength = 8;
            if (value.Length < MinLength)
                throw new DomainException($"{nameof(PlainPassword)} must be at least {MinLength} characters long");

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