using LifeManager.Domain.Exceptions;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.Errors;

namespace LifeManager.Domain.Users.ValueObjects
{
    public sealed class Email
    {
        public string Value { get; }

        private Email(string value)
        {
            Value = value;
        }

        public static Result<Email> Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return UserErrors.EmailIsNullOrWhiteSpace;

            const string atSign = "@";
            if (!value.Contains(atSign) || value.StartsWith(atSign) || value.EndsWith(atSign))
                return UserErrors.EmailIsInvalid;

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