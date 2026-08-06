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
        public bool IsRevoked { get; private set; }

        private RefreshToken(
            UserId userId,
            RefreshTokenHash tokenHash,
            DateTimeOffset expiresAt,
            bool isRevoked)
        {
            UserId = userId;
            TokenHash = tokenHash;
            ExpiresAt = expiresAt;
            IsRevoked = isRevoked;
        }

        public static Result<RefreshToken> Create(
            int userId,
            string tokenHash,
            DateTimeOffset date,
            bool isRevoked)
        {
            return RefreshTokenHash.Create(tokenHash)
                .Map(tokenHash =>
                {
                    var idUser = new UserId(userId);
                    return new RefreshToken(idUser, tokenHash, date, isRevoked);
                });
        }

        public void RevokeToken()
        {
            IsRevoked = true;
        }

        public void AssignId(int id)
        {
            Id = new RefreshTokenId(id);
        }
    }
}
