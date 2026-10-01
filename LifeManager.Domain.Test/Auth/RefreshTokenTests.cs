using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Test.Auth
{
    public class RefreshTokenTests
    {
        [Fact]
        public void Create_ShouldReturnFailure_WhenTokenHashIsInvalid()
        {
            var result = RefreshToken.Create(1, string.Empty, DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(30), false);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.RefreshTokenHashIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnRefreshToken_WhenValuesAreValid()
        {
            var userId = 1;
            var tokenHash = "hash";
            var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
            var isRevoked = false;

            var result = RefreshToken.Create(userId, tokenHash, expiresAt, expiresAt.AddDays(23), isRevoked);
            var refreshToken = result.Value;

            Assert.NotNull(refreshToken);
            Assert.IsType<RefreshToken>(refreshToken);
            Assert.Null(refreshToken.Id);
            Assert.Equal(userId, refreshToken.UserId.Value);
            Assert.Equal(tokenHash, refreshToken.TokenHash.Value);
            Assert.Equal(expiresAt, refreshToken.ExpiresAt);
            Assert.Equal(expiresAt.AddDays(23), refreshToken.SessionExpiresAt);
            Assert.Equal(isRevoked, refreshToken.IsRevoked);
        }

        [Fact]
        public void Create_ShouldCapExpiresAtToSessionExpiresAt_WhenExpiresAtIsAfterTheSessionCap()
        {
            var sessionExpiresAt = DateTimeOffset.UtcNow.AddDays(2);

            var refreshToken = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), sessionExpiresAt, false).Value!;

            Assert.Equal(sessionExpiresAt, refreshToken.ExpiresAt);
            Assert.Equal(sessionExpiresAt, refreshToken.SessionExpiresAt);
        }

        [Fact]
        public void Rotate_ShouldSlideExpirationAndKeepSession_WhenSessionCapIsFarAway()
        {
            var now = DateTimeOffset.UtcNow;
            var sessionExpiresAt = now.AddDays(30);
            var currentToken = RefreshToken.Create(1, "hash", now.AddDays(1), sessionExpiresAt, false).Value!;

            var result = currentToken.Rotate("new-hash", now, TimeSpan.FromDays(7));

            Assert.True(result.IsSuccess);
            Assert.Equal(currentToken.UserId, result.Value.UserId);
            Assert.Equal("new-hash", result.Value.TokenHash.Value);
            Assert.Equal(now.AddDays(7), result.Value.ExpiresAt);
            Assert.Equal(sessionExpiresAt, result.Value.SessionExpiresAt);
            Assert.False(result.Value.IsRevoked);
        }

        [Fact]
        public void Rotate_ShouldCapExpirationAtSessionExpiresAt_WhenSessionCapIsCloserThanTheSlidingLifetime()
        {
            var now = DateTimeOffset.UtcNow;
            var sessionExpiresAt = now.AddDays(3);
            var currentToken = RefreshToken.Create(1, "hash", now.AddDays(1), sessionExpiresAt, false).Value!;

            var result = currentToken.Rotate("new-hash", now, TimeSpan.FromDays(7));

            Assert.True(result.IsSuccess);
            Assert.Equal(sessionExpiresAt, result.Value.ExpiresAt);
        }

        [Fact]
        public void Rotate_ShouldReturnFailure_WhenNewTokenHashIsInvalid()
        {
            var currentToken = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(30), false).Value!;

            var result = currentToken.Rotate(" ", DateTimeOffset.UtcNow, TimeSpan.FromDays(7));

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.RefreshTokenHashIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void IsActive_ShouldReturnTrue_WhenTokenIsNotRevokedAndNotExpired()
        {
            var refreshToken = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(30), false).Value!;

            Assert.True(refreshToken.IsActive(DateTimeOffset.UtcNow));
        }

        [Fact]
        public void IsActive_ShouldReturnFalse_WhenTokenIsRevoked()
        {
            var refreshToken = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(30), true).Value!;

            Assert.False(refreshToken.IsActive(DateTimeOffset.UtcNow));
        }

        [Fact]
        public void IsActive_ShouldReturnFalse_WhenTokenIsExpired()
        {
            var refreshToken = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30), false).Value!;

            Assert.False(refreshToken.IsActive(DateTimeOffset.UtcNow));
        }

        [Fact]
        public void RevokeToken_ShouldSetIsRevokedToTrue_WhenTokenIsNotRevoked()
        {
            var result = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(30), false);
            var refreshToken = result.Value;

            refreshToken!.RevokeToken();

            Assert.True(refreshToken.IsRevoked);
        }

        [Fact]
        public void RevokeToken_ShouldKeepIsRevokedTrue_WhenTokenIsAlreadyRevoked()
        {
            var result = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(30), true);
            var refreshToken = result.Value;

            refreshToken!.RevokeToken();

            Assert.True(refreshToken.IsRevoked);
        }

        [Fact]
        public void AssignId_ShouldSetId_WhenRefreshTokenHasNoIdYet()
        {
            const short id = 10;

            var result = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(30), false);
            var refreshToken = result.Value;

            refreshToken!.AssignId(id);

            Assert.NotNull(refreshToken.Id);
            Assert.Equal(id, refreshToken.Id!.Value);
        }

        [Fact]
        public void Equals_ShouldReturnTrue_WhenTokensHaveTheSameId()
        {
            var token1 = RefreshToken.Create(1, "hash1", DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(30), false).Value!;
            var token2 = RefreshToken.Create(2, "hash2", DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(30), true).Value!;
            token1.AssignId(1);
            token2.AssignId(1);

            Assert.Equal(token1, token2);
            Assert.Equal(token1.GetHashCode(), token2.GetHashCode());
        }

        [Fact]
        public void Equals_ShouldReturnFalse_WhenTokensHaveDifferentIds()
        {
            var token1 = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(30), false).Value!;
            var token2 = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(30), false).Value!;
            token1.AssignId(1);
            token2.AssignId(2);

            Assert.NotEqual(token1, token2);
        }

        [Fact]
        public void Equals_ShouldReturnFalse_WhenNeitherTokenHasBeenAssignedAnId()
        {
            var token1 = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(30), false).Value!;
            var token2 = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(30), false).Value!;

            Assert.NotEqual(token1, token2);
            Assert.Equal(token1, token1);
        }
    }
}
