using LifeManager.Application.Auth.Services;
using LifeManager.Application.Test.Configurations;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Auth
{
    [Collection("ApplicationServices")]
    public class AuthServiceTest : BaseTest
    {
        private readonly AuthService _authService;

        public AuthServiceTest()
        {
            _authService = ServiceProvider.GetRequiredService<AuthService>();
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
    }
}
