using LifeManager.Domain.Auth.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Auth.ValueObjects
{
    public class RefreshTokenHash
    {
        public string Value { get; }

        private RefreshTokenHash(string value)
        {
            Value = value;
        }

        public static Result<RefreshTokenHash> Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return AuthErrors.RefreshTokenHashIsNullOrWhiteSpace;

            return new RefreshTokenHash(value);
        }

        public static RefreshTokenHash FromPersistence(string value) => new(value);

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