using LifeManager.Domain.Auth.Enums;

namespace LifeManager.Domain.Auth
{
    /// <summary>
    /// When an auth e-mail of a kind last went to an address, so the same e-mail goes out at most once per
    /// <see cref="Duration"/> (someone resubmitting a form can't flood a mailbox). Keyed by the lowercase address and the
    /// kind; it holds addresses without an account too, so it has no link to the user.
    /// </summary>
    public sealed class EmailCooldown
    {
        public static readonly TimeSpan Duration = TimeSpan.FromSeconds(60);

        /// <summary>Rows older than this are deleted by the hourly cleanup: past the cooldown they decide nothing.</summary>
        public static readonly TimeSpan Retention = TimeSpan.FromDays(1);

        public string Email { get; private set; }
        public EmailKind Kind { get; private set; }
        public DateTimeOffset LastSentAt { get; private set; }

        private EmailCooldown(string email, EmailKind kind, DateTimeOffset lastSentAt)
        {
            Email = email;
            Kind = kind;
            LastSentAt = lastSentAt;
        }

        public static EmailCooldown Create(string email, EmailKind kind, DateTimeOffset sentAt)
            => new(KeyOf(email), kind, sentAt);

        public static string KeyOf(string email) => email.ToLowerInvariant();

        public bool CanSendAgain(DateTimeOffset now) => LastSentAt <= now - Duration;

        public void MarkSent(DateTimeOffset now) => LastSentAt = now;
    }
}
