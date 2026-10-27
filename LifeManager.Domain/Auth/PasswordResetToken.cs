using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Auth
{
    /// <summary>
    /// A password reset link sent by e-mail: the hash of its token, valid for <see cref="ValidityHours"/> and only once.
    /// A new request replaces the user's previous links.
    /// </summary>
    public class PasswordResetToken
    {
        public const int ValidityHours = 1;

        /// <summary>Days a used or expired link is kept before the hourly cleanup deletes it.</summary>
        public const int RetentionDays = 7;

        public PasswordResetTokenId? Id { get; private set; }
        public UserId UserId { get; private set; }
        public EmailTokenHash TokenHash { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset ExpiresAt { get; private set; }
        public DateTimeOffset? UsedAt { get; private set; }

        private PasswordResetToken(UserId userId, EmailTokenHash tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
        {
            UserId = userId;
            TokenHash = tokenHash;
            CreatedAt = createdAt;
            ExpiresAt = expiresAt;
        }

        public static PasswordResetToken Create(UserId userId, EmailTokenHash tokenHash, DateTimeOffset now)
            => new(userId, tokenHash, now, now.AddHours(ValidityHours));

        public bool IsUsable(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;

        public void MarkUsed(DateTimeOffset now)
        {
            UsedAt = now;
        }

        public void AssignId(int id)
        {
            Id = new PasswordResetTokenId(id);
        }
    }
}
