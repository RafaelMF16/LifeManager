using LifeManager.Application.Auth;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace LifeManager.Application.Test.Auth
{
    [Collection("ApplicationServices")]
    public class AuthServiceTest : BaseTest
    {
        private readonly AuthService _authService;

        public AuthServiceTest()
        {
            _authService = ServiceProvider.GetRequiredService<AuthService>();

            RefreshTokenSingleton.Instance.Clear();
        }

        [Fact]
        public void EncryptPassword_ShouldReturnHashedPassword_WhenPasswordIsValid()
        {
            const string password = "password";
            var hashedPassword = _authService.EncryptPassword(password);

            Assert.NotNull(hashedPassword);
            Assert.NotEqual(password, hashedPassword);
        }

        [Fact]
        public void VerifyPassword_ShouldReturnTrue_WhenPasswordMatchesHash()
        {
            const string password = "password";
            var hashedPassword = _authService.EncryptPassword(password);

            var result = _authService.VerifyPassword(password, hashedPassword);

            Assert.True(result);
        }

        [Fact]
        public void VerifyPassword_ShouldReturnFalse_WhenPasswordDoesNotMatchHash()
        {
            const string password = "password";
            const string wrongPassword = "wrongPassword";
            var hashedPassword = _authService.EncryptPassword(password);

            var result = _authService.VerifyPassword(wrongPassword, hashedPassword);

            Assert.False(result);
        }

        [Fact]
        public void GenerateTokens_ShouldReturnAccessAndRefreshTokens_WhenUserIdIsValid()
        {
            const int userId = 1;
            var tokens = _authService.GenerateTokens(userId);

            Assert.NotNull(tokens);
            Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
            Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        }

        [Fact]
        public void GenerateTokens_ShouldEncodeUserIdInAccessToken_WhenUserIdIsValid()
        {
            const int userId = 42;
            var tokens = _authService.GenerateTokens(userId);

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
            _authService.GenerateTokens(userId);

            Assert.Single(RefreshTokenSingleton.Instance);
            Assert.Equal(userId, RefreshTokenSingleton.Instance[0].UserId.Value);
            Assert.False(RefreshTokenSingleton.Instance[0].IsRevoked.Value);
        }
    }
}
