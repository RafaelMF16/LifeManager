using LifeManager.Domain.Exceptions;

namespace LifeManager.Domain.Auth.ValueObjects
{
    public class RefreshTokenHash
    {
        public string Value { get; }

        private RefreshTokenHash(string value)
        {
            Value = value;
        }

        public static RefreshTokenHash Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException($"{nameof(RefreshTokenHash)} is required");

            return new RefreshTokenHash(value);
        }

        public override bool Equals(object? obj)
        {
            if (obj is RefreshTokenHash other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}