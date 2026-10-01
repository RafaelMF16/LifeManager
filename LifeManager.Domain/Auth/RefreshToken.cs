using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Auth
{
    public class RefreshToken
    {
        public RefreshTokenId? Id { get; private set; }
        public UserId UserId { get; }
        public RefreshTokenHash TokenHash { get; private set; }
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset ExpiresAt { get; private set; }

        /// <summary>Absolute session cap set at login and inherited by every rotation; ExpiresAt never goes past it.</summary>
        public DateTimeOffset SessionExpiresAt { get; private set; }
        public bool IsRevoked { get; private set; }

        private RefreshToken(
            UserId userId,
            RefreshTokenHash tokenHash,
            DateTimeOffset expiresAt,
            DateTimeOffset sessionExpiresAt,
            bool isRevoked)
        {
            UserId = userId;
            TokenHash = tokenHash;
            ExpiresAt = expiresAt;
            SessionExpiresAt = sessionExpiresAt;
            IsRevoked = isRevoked;
        }

        public static Result<RefreshToken> Create(
            int userId,
            string tokenHash,
            DateTimeOffset expiresAt,
            DateTimeOffset sessionExpiresAt,
            bool isRevoked)
        {
            return RefreshTokenHash.Create(tokenHash)
                .Map(tokenHash =>
                {
                    var idUser = new UserId(userId);
                    var cappedExpiresAt = expiresAt < sessionExpiresAt ? expiresAt : sessionExpiresAt;
                    return new RefreshToken(idUser, tokenHash, cappedExpiresAt, sessionExpiresAt, isRevoked);
                });
        }

        /// <summary>Creates the successor token of the same session: slides ExpiresAt, capped by SessionExpiresAt.</summary>
        public Result<RefreshToken> Rotate(string newTokenHash, DateTimeOffset now, TimeSpan slidingLifetime)
            => Create(UserId.Value, newTokenHash, now.Add(slidingLifetime), SessionExpiresAt, false);

        public bool IsActive(DateTimeOffset now)
            => !IsRevoked && ExpiresAt > now;

        public void RevokeToken()
        {
            IsRevoked = true;
        }

        public void AssignId(int id)
        {
            Id = new RefreshTokenId(id);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not RefreshToken other)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Id is null || other.Id is null)
                return false;

            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
            => Id?.GetHashCode() ?? base.GetHashCode();
    }
}
