using System.Collections.Concurrent;

namespace LifeManager.Application.Auth.Services
{
    public class AuthService(PasswordHashingOptions options)
    {
        // One hash per cost, made on first use: verifying against it costs the same as verifying a real password.
        private static readonly ConcurrentDictionary<int, string> DummyHashes = new();

        private readonly int _workFactor = options.WorkFactor;

        public string EncryptPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, _workFactor);
        }

        /// <summary>
        /// Without a hash (no account for the e-mail) it still runs BCrypt, against a fixed hash, and returns false, so a
        /// login takes as long whether or not the account exists.
        /// </summary>
        public bool VerifyPassword(string password, string? hashedPassword)
        {
            if (hashedPassword is null)
            {
                BCrypt.Net.BCrypt.Verify(password, DummyHashes.GetOrAdd(_workFactor, workFactor => BCrypt.Net.BCrypt.HashPassword("dummy-password", workFactor)));
                return false;
            }

            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }
}
