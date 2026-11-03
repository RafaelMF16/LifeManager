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

        [Fact]
        public void EncryptPassword_ShouldUseConfiguredWorkFactor()
        {
            var hashedPassword = _authService.EncryptPassword("password");

            Assert.Equal(TestWorkFactor, WorkFactorOf(hashedPassword));
        }

        [Fact]
        public void EncryptPassword_ShouldUseWorkFactor12_ByDefault()
        {
            var authService = new AuthService(new PasswordHashingOptions());

            Assert.Equal(12, WorkFactorOf(authService.EncryptPassword("password")));
        }

        [Fact]
        public void VerifyPassword_ShouldReturnFalse_WhenThereIsNoHash()
        {
            var result = _authService.VerifyPassword("password", null);

            Assert.False(result);
        }

        private static int TestWorkFactor => InjectionModule.TestPasswordWorkFactor;

        // A BCrypt hash is "$2b$<cost>$<salt+hash>".
        private static int WorkFactorOf(string hashedPassword) => int.Parse(hashedPassword.Split('$')[2]);
    }
}
