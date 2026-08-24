using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.Errors;

namespace LifeManager.Domain.Users.ValueObjects
{
    public class PasswordHash
    {
        public string Value { get; }

        private PasswordHash(string value)
        {
            Value = value;
        }
        
        public static Result<PasswordHash> Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return UserErrors.PasswordHashIsNullOrWhiteSpace;

            return new PasswordHash(value);
        }

        internal static PasswordHash FromPersistence(string value) => new(value);

        public override bool Equals(object? obj)
        {
            if (obj is PasswordHash other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}