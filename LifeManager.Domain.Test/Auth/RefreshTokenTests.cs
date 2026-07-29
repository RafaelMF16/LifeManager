using LifeManager.Domain.Auth;

namespace LifeManager.Domain.Test.Auth
{
    public class RefreshTokenTests
    {
        [Fact]
        public void Create_ShouldReturnRefreshToken_WhenValuesAreValid()
        {
            var userId = 1;
            var tokenHash = "hash";
            var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
            var isRevoked = false;

            var refreshToken = RefreshToken.Create(userId, tokenHash, expiresAt, isRevoked);

            Assert.NotNull(refreshToken);
            Assert.IsType<RefreshToken>(refreshToken);
            Assert.Null(refreshToken.Id);
            Assert.Equal(userId, refreshToken.UserId.Value);
            Assert.Equal(tokenHash, refreshToken.TokenHash.Value);
            Assert.Equal(expiresAt, refreshToken.ExpiresAt);
            Assert.Equal(isRevoked, refreshToken.IsRevoked.Value);
        }

        [Fact]
        public void RevokeToken_ShouldSetIsRevokedToTrue_WhenTokenIsNotRevoked()
        {
            var refreshToken = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), false);

            refreshToken.RevokeToken();

            Assert.True(refreshToken.IsRevoked.Value);
        }

        [Fact]
        public void RevokeToken_ShouldKeepIsRevokedTrue_WhenTokenIsAlreadyRevoked()
        {
            var refreshToken = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), true);

            refreshToken.RevokeToken();

            Assert.True(refreshToken.IsRevoked.Value);
        }
    }
}
