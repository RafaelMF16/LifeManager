using LifeManager.Domain.Exceptions;

namespace LifeManager.Domain.Users
{
    public sealed class Email
    {
        public string Value { get; }

        private Email(string value)
        {
            Value = value;
        }

        public static Email Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException($"{nameof(Email)} is required");

            const string atSign = "@";
            if (!value.Contains(atSign) || value.StartsWith(atSign) || value.EndsWith(atSign))
                throw new DomainException();

            return new Email(value);
        }

        public override bool Equals(object? obj)
        {
            if (obj is Email other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}