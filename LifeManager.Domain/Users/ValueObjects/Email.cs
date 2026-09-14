using System.Text.RegularExpressions;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.Errors;

namespace LifeManager.Domain.Users.ValueObjects
{
    public sealed partial class Email
    {
        [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
        private static partial Regex Pattern();

        public string Value { get; }

        private Email(string value)
        {
            Value = value;
        }

        public static Result<Email> Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return UserErrors.EmailIsNullOrWhiteSpace;

            if (!Pattern().IsMatch(value))
                return UserErrors.EmailIsInvalid;

            return new Email(value);
        }

        internal static Email FromPersistence(string value) => new(value);

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