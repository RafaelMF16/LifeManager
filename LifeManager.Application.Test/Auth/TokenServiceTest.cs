using LifeManager.Application.Auth.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
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

            Assert.NotNull(tokens);
            Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
            Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        }

        [Fact]
        public void GenerateTokens_ShouldEncodeUserIdInAccessToken_WhenUserIdIsValid()
        {
            const int userId = 42;
            var tokens = _tokenService.GenerateTokens(userId);

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(tokens.AccessToken);
            var claimType = handler.OutboundClaimTypeMap.TryGetValue(ClaimTypes.NameIdentifier, out var mappedType)
                ? mappedType
                : ClaimTypes.NameIdentifier;
            var claim = jwt.Claims.First(c => c.Type == claimType);

            Assert.Equal(userId.ToString(), claim.Value);
        }

        [Fact]
        public void GenerateTokens_ShouldPersistRefreshToken_WhenUserIdIsValid()
        {
            const int userId = 1;
            _tokenService.GenerateTokens(userId);

            Assert.Single(RefreshTokenSingleton.Instance);
            Assert.Equal(userId, RefreshTokenSingleton.Instance[0].UserId.Value);
            Assert.False(RefreshTokenSingleton.Instance[0].IsRevoked.Value);
        }

        [Fact]
        public void GenerateTokens_ShouldRevokePreviousToken_WhenUserAlreadyHasAnActiveToken()
        {
            const int userId = 1;
            _tokenService.GenerateTokens(userId);

            _tokenService.GenerateTokens(userId);

            Assert.Equal(2, RefreshTokenSingleton.Instance.Count);
            Assert.True(RefreshTokenSingleton.Instance[0].IsRevoked.Value);
            Assert.False(RefreshTokenSingleton.Instance[1].IsRevoked.Value);
        }

        [Fact]
        public void GenerateTokens_ShouldNotAffectOtherUsersTokens_WhenGeneratingNewTokens()
        {
            const int firstUserId = 1;
            const int secondUserId = 2;
            _tokenService.GenerateTokens(firstUserId);

            _tokenService.GenerateTokens(secondUserId);

            Assert.Equal(2, RefreshTokenSingleton.Instance.Count);
            Assert.False(RefreshTokenSingleton.Instance[0].IsRevoked.Value);
            Assert.False(RefreshTokenSingleton.Instance[1].IsRevoked.Value);
        }
    }
}
