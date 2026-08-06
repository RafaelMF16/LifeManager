using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Auth.ValueObjects
{
    public class RefreshTokenRevoked
    {
        public bool Value { get; private set; }

        private RefreshTokenRevoked(bool value)
        {
            Value = value;
        }

        public static RefreshTokenRevoked Create(bool value)
        {
            return new RefreshTokenRevoked(value);
        }

        public override bool Equals(object? obj)
        {
            if (obj is RefreshTokenRevoked other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}