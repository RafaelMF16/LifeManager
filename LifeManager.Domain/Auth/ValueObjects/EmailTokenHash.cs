using LifeManager.Domain.Auth.Errors;
using LifeManager.Domain.Shared.Results;
using System.Security.Cryptography;
using System.Text;

namespace LifeManager.Domain.Auth.ValueObjects
{
    /// <summary>
    /// The stored form of a token sent by e-mail (sign-up confirmation, password reset): its SHA-256 in lowercase hex.
    /// The token is 32 random bytes, so a plain hash is enough (no secret or slow hash needed) and is looked up directly.
    /// </summary>
    public sealed class EmailTokenHash
    {
        public string Value { get; }

        private EmailTokenHash(string value)
        {
            Value = value;
        }

        public static Result<EmailTokenHash> FromToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return AuthErrors.InvalidEmailToken;

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return new EmailTokenHash(Convert.ToHexStringLower(hash));
        }

        internal static EmailTokenHash FromPersistence(string value) => new(value);

        public override bool Equals(object? obj)
            => obj is EmailTokenHash other && Value == other.Value;

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
