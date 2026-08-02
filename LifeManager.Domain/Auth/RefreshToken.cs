using LifeManager.Domain.Auth.ValueObjects;
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
        public RefreshTokenRevoked IsRevoked { get; private set; }

        private RefreshToken(
            UserId userId,
            RefreshTokenHash tokenHash,
            DateTimeOffset expiresAt,
            RefreshTokenRevoked isRevoked)
        {
            UserId = userId;
            TokenHash = tokenHash;
            ExpiresAt = expiresAt;
            IsRevoked = isRevoked;
        }

        public static RefreshToken Create(
            int userId,
            string tokenHash,
            DateTimeOffset date,
            bool isRevoked)
        {
            var idUser = new UserId(userId);
            var refreshTokenHash = RefreshTokenHash.Create(tokenHash);
            var refreshTokenRevoked = RefreshTokenRevoked.Create(isRevoked);

            return new RefreshToken(idUser, refreshTokenHash, date, refreshTokenRevoked);
        }

        public void RevokeToken()
        {
            IsRevoked = RefreshTokenRevoked.Create(true);
        }

        public void AssignId(int id)
        {
            Id = new RefreshTokenId(id);
        }
    }
}
