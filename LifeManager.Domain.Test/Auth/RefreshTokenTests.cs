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
            var result = RefreshToken.Create(1, string.Empty, DateTimeOffset.UtcNow.AddDays(7), false);

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

            var result = RefreshToken.Create(userId, tokenHash, expiresAt, isRevoked);
            var refreshToken = result.Value;

            Assert.NotNull(refreshToken);
            Assert.IsType<RefreshToken>(refreshToken);
            Assert.Null(refreshToken.Id);
            Assert.Equal(userId, refreshToken.UserId.Value);
            Assert.Equal(tokenHash, refreshToken.TokenHash.Value);
            Assert.Equal(expiresAt, refreshToken.ExpiresAt);
            Assert.Equal(isRevoked, refreshToken.IsRevoked);
        }

        [Fact]
        public void RevokeToken_ShouldSetIsRevokedToTrue_WhenTokenIsNotRevoked()
        {
            var result = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), false);
            var refreshToken = result.Value;

            refreshToken!.RevokeToken();

            Assert.True(refreshToken.IsRevoked);
        }

        [Fact]
        public void RevokeToken_ShouldKeepIsRevokedTrue_WhenTokenIsAlreadyRevoked()
        {
            var result = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), true);
            var refreshToken = result.Value;

            refreshToken!.RevokeToken();

            Assert.True(refreshToken.IsRevoked);
        }

        [Fact]
        public void IsActive_ShouldBeTrue_WhenTokenIsNotRevokedAndNotExpired()
        {
            var result = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), false);
            var refreshToken = result.Value;

            Assert.True(refreshToken!.IsActive);
        }

        [Fact]
        public void IsActive_ShouldBeFalse_WhenTokenIsRevoked()
        {
            var result = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), true);
            var refreshToken = result.Value;

            Assert.False(refreshToken!.IsActive);
        }

        [Fact]
        public void IsActive_ShouldBeFalse_WhenTokenIsExpired()
        {
            var result = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(-1), false);
            var refreshToken = result.Value;

            Assert.False(refreshToken!.IsActive);
        }

        [Fact]
        public void AssignId_ShouldSetId_WhenRefreshTokenHasNoIdYet()
        {
            const short id = 10;

            var result = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), false);
            var refreshToken = result.Value;

            refreshToken!.AssignId(id);

            Assert.NotNull(refreshToken.Id);
            Assert.Equal(id, refreshToken.Id!.Value);
        }

        [Fact]
        public void Equals_ShouldReturnTrue_WhenTokensHaveTheSameId()
        {
            var token1 = RefreshToken.Create(1, "hash1", DateTimeOffset.UtcNow.AddDays(7), false).Value!;
            var token2 = RefreshToken.Create(2, "hash2", DateTimeOffset.UtcNow.AddDays(7), true).Value!;
            token1.AssignId(1);
            token2.AssignId(1);

            Assert.Equal(token1, token2);
            Assert.Equal(token1.GetHashCode(), token2.GetHashCode());
        }

        [Fact]
        public void Equals_ShouldReturnFalse_WhenTokensHaveDifferentIds()
        {
            var token1 = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), false).Value!;
            var token2 = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), false).Value!;
            token1.AssignId(1);
            token2.AssignId(2);

            Assert.NotEqual(token1, token2);
        }

        [Fact]
        public void Equals_ShouldReturnFalse_WhenNeitherTokenHasBeenAssignedAnId()
        {
            var token1 = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), false).Value!;
            var token2 = RefreshToken.Create(1, "hash", DateTimeOffset.UtcNow.AddDays(7), false).Value!;

            Assert.NotEqual(token1, token2);
            Assert.Equal(token1, token1);
        }
    }
}
