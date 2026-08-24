using LifeManager.Application.Auth.Services;
using LifeManager.Application.EnvironmentVariables.Errors;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Auth;
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
            var expiredToken = RefreshToken.Create(userId, "expired-hash", DateTimeOffset.UtcNow.AddDays(-1), false).Value;
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
    }
}
