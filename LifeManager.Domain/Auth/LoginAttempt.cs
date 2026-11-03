namespace LifeManager.Domain.Auth
{
    /// <summary>
    /// Wrong passwords for an e-mail from one IP inside the current window. <see cref="MaxFailures"/> of them block that
    /// pair until the window ends, so a password can't be guessed from one address, while the owner coming from another
    /// one still logs in. E-mails without an account count too (it holds no link to the user), so a block never tells
    /// whether the account exists.
    /// </summary>
    public sealed class LoginAttempt
    {
        public const int MaxFailures = 5;
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

        /// <summary>Rows whose window ended longer ago than this are deleted by the hourly cleanup.</summary>
        public static readonly TimeSpan Retention = TimeSpan.FromDays(1);

        public string Email { get; private set; }
        public string IpAddress { get; private set; }
        public int FailedCount { get; private set; }
        public DateTimeOffset WindowStartedAt { get; private set; }

        private LoginAttempt(string email, string ipAddress, int failedCount, DateTimeOffset windowStartedAt)
        {
            Email = email;
            IpAddress = ipAddress;
            FailedCount = failedCount;
            WindowStartedAt = windowStartedAt;
        }

        /// <summary>The first wrong password of a window.</summary>
        public static LoginAttempt Create(string email, string ipAddress, DateTimeOffset failedAt)
            => new(KeyOf(email), ipAddress, 1, failedAt);

        public static string KeyOf(string email) => email.ToLowerInvariant();

        public bool IsWindowOver(DateTimeOffset now) => WindowStartedAt <= now - Window;

        public bool IsBlocked(DateTimeOffset now) => FailedCount >= MaxFailures && !IsWindowOver(now);

        public void RecordFailure(DateTimeOffset now)
        {
            if (IsWindowOver(now))
            {
                FailedCount = 1;
                WindowStartedAt = now;
                return;
            }

            FailedCount++;
        }
    }
}
