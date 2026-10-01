using LifeManager.Application.Auth.Services;
using LifeManager.Application.EnvironmentVariables.Errors;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Errors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace LifeManager.Application.Test.Auth
{
    [Collection("ApplicationServices")]
    public class TokenServiceTest : BaseTest
    {
        private readonly TokenService _tokenService;

        public TokenServiceTest()
        {
            _tokenService = ServiceProvider.GetRequiredService<TokenService>();

            RefreshTokenSingleton.Instance.Clear();
        }

        [Fact]
        public void GenerateTokens_ShouldReturnAccessAndRefreshTokens_WhenUserIdIsValid()
        {
            const int userId = 1;
            var tokens = _tokenService.GenerateTokens(userId);

            Assert.True(tokens.IsSuccess);
            Assert.False(string.IsNullOrWhiteSpace(tokens.Value.AccessToken));
            Assert.False(string.IsNullOrWhiteSpace(tokens.Value.RefreshToken));
        }

        [Fact]
        public void GenerateTokens_ShouldEncodeUserIdInAccessToken_WhenUserIdIsValid()
        {
            const int userId = 42;
            var tokens = _tokenService.GenerateTokens(userId);

            Assert.True(tokens.IsSuccess);
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(tokens.Value.AccessToken);
            var claimType = handler.OutboundClaimTypeMap.TryGetValue(ClaimTypes.NameIdentifier, out var mappedType)
                ? mappedType
                : ClaimTypes.NameIdentifier;
            var claim = jwt.Claims.First(c => c.Type == claimType);

            Assert.Equal(userId.ToString(), claim.Value);
        }

        [Fact]
        public void GenerateTokens_ShouldSetAccessTokenExpiration_WhenUserIdIsValid()
        {
            const int userId = 1;
            var expectedExpiration = DateTime.UtcNow.AddMinutes(15);

            var tokens = _tokenService.GenerateTokens(userId);

            Assert.True(tokens.IsSuccess);
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(tokens.Value.AccessToken);

            Assert.True(Math.Abs((jwt.ValidTo - expectedExpiration).TotalSeconds) < 5);
        }

        [Fact]
        public void GenerateTokens_ShouldPersistRefreshToken_WhenUserIdIsValid()
        {
            const int userId = 1;
            _tokenService.GenerateTokens(userId);

            Assert.Single(RefreshTokenSingleton.Instance);
            Assert.Equal(userId, RefreshTokenSingleton.Instance[0].UserId.Value);
            Assert.False(RefreshTokenSingleton.Instance[0].IsRevoked);
        }

        [Fact]
        public void GenerateTokens_ShouldRevokePreviousToken_WhenUserAlreadyHasAnActiveToken()
        {
            const int userId = 1;
            _tokenService.GenerateTokens(userId);

            _tokenService.GenerateTokens(userId);

            Assert.Equal(2, RefreshTokenSingleton.Instance.Count);
            Assert.True(RefreshTokenSingleton.Instance[0].IsRevoked);
            Assert.False(RefreshTokenSingleton.Instance[1].IsRevoked);
        }

        [Fact]
        public void GenerateTokens_ShouldNotRevokeExpiredToken_WhenPreviousTokenIsExpired()
        {
            const int userId = 1;
            var expiredToken = RefreshToken.Create(userId, "expired-hash", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(29), false).Value;
            RefreshTokenSingleton.Instance.Add(expiredToken!);

            _tokenService.GenerateTokens(userId);

            Assert.Equal(2, RefreshTokenSingleton.Instance.Count);
            Assert.False(RefreshTokenSingleton.Instance[0].IsRevoked);
            Assert.False(RefreshTokenSingleton.Instance[1].IsRevoked);
        }

        [Fact]
        public void GenerateTokens_ShouldNotAffectOtherUsersTokens_WhenGeneratingNewTokens()
        {
            const int firstUserId = 1;
            const int secondUserId = 2;
            _tokenService.GenerateTokens(firstUserId);

            _tokenService.GenerateTokens(secondUserId);

            Assert.Equal(2, RefreshTokenSingleton.Instance.Count);
            Assert.False(RefreshTokenSingleton.Instance[0].IsRevoked);
            Assert.False(RefreshTokenSingleton.Instance[1].IsRevoked);
        }

        [Fact]
        public void GenerateTokens_ShouldReturnFailure_WhenAccessTokenSecretKeyIsMissing()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["refreshTokenSecretKey"] = "test-refresh-token-secret-key-0123456789abcdef"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddServicesInScope(configuration);
            var tokenService = services.BuildServiceProvider().GetRequiredService<TokenService>();

            var result = tokenService.GenerateTokens(1);

            Assert.False(result.IsSuccess);
            Assert.Equal(EnvironmentVariableErrors.KeyNotFound("accessTokenSecretKey"), result.Error);
        }

        [Fact]
        public async Task RevokeRefreshTokenAsync_ShouldRevokeToken_WhenTokenIsActive()
        {
            var tokens = _tokenService.GenerateTokens(1);

            var result = await _tokenService.RevokeRefreshTokenAsync(tokens.Value!.RefreshToken, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(RefreshTokenSingleton.Instance[0].IsRevoked);
        }

        [Fact]
        public async Task RevokeRefreshTokenAsync_ShouldSucceed_WhenTokenDoesNotExist()
        {
            _tokenService.GenerateTokens(1);

            var result = await _tokenService.RevokeRefreshTokenAsync(Guid.NewGuid().ToString(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.False(RefreshTokenSingleton.Instance[0].IsRevoked);
        }

        [Fact]
        public async Task RevokeRefreshTokenAsync_ShouldSucceed_WhenTokenIsAlreadyRevoked()
        {
            var tokens = _tokenService.GenerateTokens(1);
            await _tokenService.RevokeRefreshTokenAsync(tokens.Value!.RefreshToken, CancellationToken.None);

            var result = await _tokenService.RevokeRefreshTokenAsync(tokens.Value.RefreshToken, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(RefreshTokenSingleton.Instance[0].IsRevoked);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task RevokeRefreshTokenAsync_ShouldSucceed_WhenTokenIsNullOrWhiteSpace(string? refreshToken)
        {
            _tokenService.GenerateTokens(1);

            var result = await _tokenService.RevokeRefreshTokenAsync(refreshToken, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.False(RefreshTokenSingleton.Instance[0].IsRevoked);
        }

        [Fact]
        public async Task RevokeRefreshTokenAsync_ShouldNotAffectOtherUsersTokens_WhenRevoking()
        {
            var firstUserTokens = _tokenService.GenerateTokens(1);
            _tokenService.GenerateTokens(2);

            await _tokenService.RevokeRefreshTokenAsync(firstUserTokens.Value!.RefreshToken, CancellationToken.None);

            Assert.True(RefreshTokenSingleton.Instance[0].IsRevoked);
            Assert.False(RefreshTokenSingleton.Instance[1].IsRevoked);
        }

        [Fact]
        public async Task RevokeRefreshTokenAsync_ShouldReturnFailure_WhenRefreshTokenSecretKeyIsMissing()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["accessTokenSecretKey"] = "test-access-token-secret-key-0123456789abcdef"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddServicesInScope(configuration);
            var tokenService = services.BuildServiceProvider().GetRequiredService<TokenService>();

            var result = await tokenService.RevokeRefreshTokenAsync(Guid.NewGuid().ToString(), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(EnvironmentVariableErrors.KeyNotFound("refreshTokenSecretKey"), result.Error);
        }

        [Fact]
        public void GenerateTokens_ShouldStartSessionWithThirtyDayCap_WhenUserLogsIn()
        {
            var tokens = _tokenService.GenerateTokens(1);

            var storedToken = RefreshTokenSingleton.Instance[0];
            Assert.True(Math.Abs((storedToken.SessionExpiresAt - DateTimeOffset.UtcNow.AddDays(30)).TotalSeconds) < 5);
            Assert.True(Math.Abs((storedToken.ExpiresAt - DateTimeOffset.UtcNow.AddDays(7)).TotalSeconds) < 5);
            Assert.Equal(storedToken.ExpiresAt, tokens.Value!.RefreshTokenExpiresAt);
        }

        [Fact]
        public async Task RefreshTokensAsync_ShouldRotateTokens_WhenRefreshTokenIsActive()
        {
            var loginTokens = _tokenService.GenerateTokens(1).Value!;
            var sessionExpiresAt = RefreshTokenSingleton.Instance[0].SessionExpiresAt;

            var result = await _tokenService.RefreshTokensAsync(loginTokens.RefreshToken, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.False(string.IsNullOrWhiteSpace(result.Value.AccessToken));
            Assert.NotEqual(loginTokens.RefreshToken, result.Value.RefreshToken);
            Assert.Equal(2, RefreshTokenSingleton.Instance.Count);
            Assert.True(RefreshTokenSingleton.Instance[0].IsRevoked);

            var rotatedToken = RefreshTokenSingleton.Instance[1];
            Assert.False(rotatedToken.IsRevoked);
            Assert.Equal(1, rotatedToken.UserId.Value);
            Assert.Equal(sessionExpiresAt, rotatedToken.SessionExpiresAt);
            Assert.True(Math.Abs((rotatedToken.ExpiresAt - DateTimeOffset.UtcNow.AddDays(7)).TotalSeconds) < 5);
            Assert.Equal(rotatedToken.ExpiresAt, result.Value.RefreshTokenExpiresAt);
        }

        [Fact]
        public async Task RefreshTokensAsync_ShouldEncodeSameUserIdInNewAccessToken_WhenRefreshTokenIsActive()
        {
            const int userId = 42;
            var loginTokens = _tokenService.GenerateTokens(userId).Value!;

            var result = await _tokenService.RefreshTokensAsync(loginTokens.RefreshToken, CancellationToken.None);

            var handler = new JwtSecurityTokenHandler();
            var claimType = handler.OutboundClaimTypeMap.TryGetValue(ClaimTypes.NameIdentifier, out var mappedType)
                ? mappedType
                : ClaimTypes.NameIdentifier;
            var claim = handler.ReadJwtToken(result.Value!.AccessToken).Claims.First(c => c.Type == claimType);

            Assert.Equal(userId.ToString(), claim.Value);
        }

        [Fact]
        public async Task RefreshTokensAsync_ShouldAllowChainedRotations_WhenAlwaysUsingTheLatestToken()
        {
            var tokens = _tokenService.GenerateTokens(1).Value!;

            var firstRefresh = await _tokenService.RefreshTokensAsync(tokens.RefreshToken, CancellationToken.None);
            var secondRefresh = await _tokenService.RefreshTokensAsync(firstRefresh.Value!.RefreshToken, CancellationToken.None);

            Assert.True(secondRefresh.IsSuccess);
            Assert.Equal(3, RefreshTokenSingleton.Instance.Count);
            Assert.Single(RefreshTokenSingleton.Instance, token => !token.IsRevoked);
        }

        [Fact]
        public async Task RefreshTokensAsync_ShouldCapExpirationAtSessionLimit_WhenSessionIsAboutToEnd()
        {
            var tokens = _tokenService.GenerateTokens(1).Value!;
            var loginToken = RefreshTokenSingleton.Instance[0];
            var sessionExpiresAt = DateTimeOffset.UtcNow.AddDays(2);
            RefreshTokenSingleton.Instance[0] = RefreshToken.Create(1, loginToken.TokenHash.Value, DateTimeOffset.UtcNow.AddDays(1), sessionExpiresAt, false).Value!;

            var result = await _tokenService.RefreshTokensAsync(tokens.RefreshToken, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(sessionExpiresAt, RefreshTokenSingleton.Instance[1].ExpiresAt);
            Assert.Equal(sessionExpiresAt, result.Value.RefreshTokenExpiresAt);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task RefreshTokensAsync_ShouldReturnInvalidRefreshToken_WhenTokenIsNullOrWhiteSpace(string? refreshToken)
        {
            var result = await _tokenService.RefreshTokensAsync(refreshToken, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
        }

        [Fact]
        public async Task RefreshTokensAsync_ShouldReturnInvalidRefreshToken_WhenTokenDoesNotExist()
        {
            _tokenService.GenerateTokens(1);

            var result = await _tokenService.RefreshTokensAsync(Guid.NewGuid().ToString(), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
            Assert.Single(RefreshTokenSingleton.Instance);
            Assert.False(RefreshTokenSingleton.Instance[0].IsRevoked);
        }

        [Fact]
        public async Task RefreshTokensAsync_ShouldReturnInvalidRefreshToken_WhenTokenIsExpired()
        {
            var tokens = _tokenService.GenerateTokens(1).Value!;
            var loginToken = RefreshTokenSingleton.Instance[0];
            RefreshTokenSingleton.Instance[0] = RefreshToken.Create(1, loginToken.TokenHash.Value, DateTimeOffset.UtcNow.AddMinutes(-1), loginToken.SessionExpiresAt, false).Value!;

            var result = await _tokenService.RefreshTokensAsync(tokens.RefreshToken, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
            Assert.Single(RefreshTokenSingleton.Instance);
        }

        [Fact]
        public async Task RefreshTokensAsync_ShouldRevokeEveryActiveToken_WhenRevokedTokenIsReused()
        {
            var loginTokens = _tokenService.GenerateTokens(1).Value!;
            var refreshed = await _tokenService.RefreshTokensAsync(loginTokens.RefreshToken, CancellationToken.None);

            var reuse = await _tokenService.RefreshTokensAsync(loginTokens.RefreshToken, CancellationToken.None);

            Assert.False(reuse.IsSuccess);
            Assert.Equal(AuthErrors.InvalidRefreshToken, reuse.Error);
            Assert.All(RefreshTokenSingleton.Instance, token => Assert.True(token.IsRevoked));

            var afterReuse = await _tokenService.RefreshTokensAsync(refreshed.Value!.RefreshToken, CancellationToken.None);
            Assert.False(afterReuse.IsSuccess);
        }

        [Fact]
        public async Task RefreshTokensAsync_ShouldNotAffectOtherUsersTokens_WhenRevokedTokenIsReused()
        {
            var firstUserTokens = _tokenService.GenerateTokens(1).Value!;
            _tokenService.GenerateTokens(2);
            await _tokenService.RefreshTokensAsync(firstUserTokens.RefreshToken, CancellationToken.None);

            await _tokenService.RefreshTokensAsync(firstUserTokens.RefreshToken, CancellationToken.None);

            Assert.False(RefreshTokenSingleton.Instance.Single(token => token.UserId.Value == 2).IsRevoked);
        }

        [Fact]
        public async Task RefreshTokensAsync_ShouldReturnInvalidRefreshToken_WhenTokenWasRevokedByLogout()
        {
            var tokens = _tokenService.GenerateTokens(1).Value!;
            await _tokenService.RevokeRefreshTokenAsync(tokens.RefreshToken, CancellationToken.None);

            var result = await _tokenService.RefreshTokensAsync(tokens.RefreshToken, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
        }

        [Fact]
        public async Task RefreshTokensAsync_ShouldReturnFailure_WhenRefreshTokenSecretKeyIsMissing()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["accessTokenSecretKey"] = "test-access-token-secret-key-0123456789abcdef"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddServicesInScope(configuration);
            var tokenService = services.BuildServiceProvider().GetRequiredService<TokenService>();

            var result = await tokenService.RefreshTokensAsync(Guid.NewGuid().ToString(), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(EnvironmentVariableErrors.KeyNotFound("refreshTokenSecretKey"), result.Error);
        }
    }
}
