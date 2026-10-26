using LifeManager.Domain.Auth.ValueObjects;
using System.Buffers.Text;
using System.Security.Cryptography;

namespace LifeManager.Application.Auth.Services
{
    /// <summary>
    /// A new token for an auth e-mail link: 32 random bytes in base64url (goes in the link, never stored) and its
    /// <see cref="EmailTokenHash"/> (stored).
    /// </summary>
    public static class EmailTokenGenerator
    {
        private const int TOKEN_BYTES = 32;

        public static (string Token, EmailTokenHash Hash) Generate()
        {
            var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TOKEN_BYTES));

            return (token, EmailTokenHash.FromToken(token).Value!);
        }
    }
}
