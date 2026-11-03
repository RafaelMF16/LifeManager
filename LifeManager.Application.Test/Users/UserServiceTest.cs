using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Auth.Services;
using LifeManager.Application.Test.Users.Mocks;
using LifeManager.Application.Users.DTOs;
using LifeManager.Application.Users.Services;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Errors;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.Errors;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Users
{
    [Collection("ApplicationServices")]
    public class UserServiceTest : BaseTest
    {
        private readonly UserService _userService;

        public UserServiceTest()
        {
            _userService = ServiceProvider.GetRequiredService<UserService>();

            UserSingleton.Instance.Clear();
            RefreshTokenSingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();
            UserPreferencesSingleton.Instance.Clear();
            LoginAttemptSingleton.Instance.Clear();
        }

        private const string TestIp = "203.0.113.7";

        private FakeTimeProvider Clock => (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();

        private async Task FailLoginsAsync(string email, int count, string ip = TestIp)
        {
            for (var attempt = 0; attempt < count; attempt++)
                await _userService.AuthenticateUserAsync(new LoginDto(email, "wrongPassword"), ip, CancellationToken.None);
        }

        private UserRepositoryMock UserRepository => (UserRepositoryMock)ServiceProvider.GetRequiredService<IUserRepository>();

        private async Task<User> AddUserAsync(string email, string name, string password)
        {
            var hashedPassword = ServiceProvider.GetRequiredService<AuthService>().EncryptPassword(password);
            var user = User.Create(name, email, hashedPassword).Value!;
            var userRepository = (UserRepositoryMock)ServiceProvider.GetRequiredService<IUserRepository>();

            return await userRepository.AddWithStarterDataAsync(user, null, null, CancellationToken.None);
        }

        [Fact]
        public async Task GetCurrentUserAsync_ShouldReturnName_WhenUserExists()
        {
            var user = await AddUserAsync("email@email.com", "name", "password");

            var result = await _userService.GetCurrentUserAsync(user.Id!, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("name", result.Value.Name);
        }

        [Fact]
        public async Task GetCurrentUserAsync_ShouldReturnNotFound_WhenUserDoesNotExist()
        {
            var result = await _userService.GetCurrentUserAsync(new UserId(999), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldReturnFailure_WhenEmailDoesNotExist()
        {
            var result = await _userService.AuthenticateUserAsync(new LoginDto("missing@email.com", "password"), TestIp, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.InvalidCredentials, result.Error);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldReturnFailure_WhenPasswordIsIncorrect()
        {
            await AddUserAsync("email@email.com", "name", "password");

            var result = await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "wrongPassword"), TestIp, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.InvalidCredentials, result.Error);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldReturnTokens_WhenCredentialsAreValid()
        {
            await AddUserAsync("email@email.com", "name", "password");

            var result = await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "password"), TestIp, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.False(string.IsNullOrWhiteSpace(result.Value.AccessToken));
            Assert.False(string.IsNullOrWhiteSpace(result.Value.RefreshToken));
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldRehashPassword_WhenStoredHashIsCheaperThanConfigured()
        {
            var user = User.Create("name", "email@email.com", BCrypt.Net.BCrypt.HashPassword("password", InjectionModule.TestPasswordWorkFactor - 1)).Value!;
            await UserRepository.AddWithStarterDataAsync(user, null, null, CancellationToken.None);

            var result = await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "password"), TestIp, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var storedHash = UserSingleton.Instance.Single().PasswordHash.Value;
            Assert.Equal(InjectionModule.TestPasswordWorkFactor, int.Parse(storedHash.Split('$')[2]));
            Assert.True(BCrypt.Net.BCrypt.Verify("password", storedHash));
            Assert.Equal(1, UserRepository.PasswordHashUpdateCount);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldNotRehashPassword_WhenStoredHashUsesConfiguredCost()
        {
            await AddUserAsync("email@email.com", "name", "password");

            await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "password"), TestIp, CancellationToken.None);

            Assert.Equal(0, UserRepository.PasswordHashUpdateCount);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldNotRehashPassword_WhenPasswordIsIncorrect()
        {
            var user = User.Create("name", "email@email.com", BCrypt.Net.BCrypt.HashPassword("password", InjectionModule.TestPasswordWorkFactor - 1)).Value!;
            await UserRepository.AddWithStarterDataAsync(user, null, null, CancellationToken.None);

            await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "wrongPassword"), TestIp, CancellationToken.None);

            Assert.Equal(0, UserRepository.PasswordHashUpdateCount);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldReturnTooManyRequests_WhenPairFailedTooManyTimes_EvenWithTheRightPassword()
        {
            await AddUserAsync("email@email.com", "name", "password");
            await FailLoginsAsync("email@email.com", LoginAttempt.MaxFailures);

            var result = await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "password"), TestIp, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.TooManyRequests, result.Error);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldStillCheckThePassword_WhenFailuresAreBelowTheLimit()
        {
            await AddUserAsync("email@email.com", "name", "password");
            await FailLoginsAsync("email@email.com", LoginAttempt.MaxFailures - 1);

            var result = await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "password"), TestIp, CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldReturnTooManyRequests_WhenEmailHasNoAccount()
        {
            await FailLoginsAsync("missing@email.com", LoginAttempt.MaxFailures);

            var result = await _userService.AuthenticateUserAsync(new LoginDto("missing@email.com", "password"), TestIp, CancellationToken.None);

            Assert.Equal(AuthErrors.TooManyRequests, result.Error);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldNotBlockTheEmail_FromAnotherIp()
        {
            await AddUserAsync("email@email.com", "name", "password");
            await FailLoginsAsync("email@email.com", LoginAttempt.MaxFailures);

            var result = await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "password"), "198.51.100.20", CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldBlockIgnoringTheEmailCase()
        {
            await AddUserAsync("email@email.com", "name", "password");
            await FailLoginsAsync("Email@Email.com", LoginAttempt.MaxFailures);

            var result = await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "password"), TestIp, CancellationToken.None);

            Assert.Equal(AuthErrors.TooManyRequests, result.Error);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldLetThePairIn_WhenTheWindowIsOver()
        {
            await AddUserAsync("email@email.com", "name", "password");
            await FailLoginsAsync("email@email.com", LoginAttempt.MaxFailures);

            Clock.UtcNow = Clock.UtcNow.Add(LoginAttempt.Window);
            var result = await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "password"), TestIp, CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldClearTheFailures_WhenLoginSucceeds()
        {
            await AddUserAsync("email@email.com", "name", "password");
            await FailLoginsAsync("email@email.com", LoginAttempt.MaxFailures - 1);

            await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "password"), TestIp, CancellationToken.None);
            await FailLoginsAsync("email@email.com", LoginAttempt.MaxFailures - 1);
            var result = await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "password"), TestIp, CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldNotCountAFailure_WhenTheEmailIsMalformed()
        {
            await _userService.AuthenticateUserAsync(new LoginDto("not-an-email", "password"), TestIp, CancellationToken.None);

            Assert.Empty(LoginAttemptSingleton.Instance);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldRevokePreviousToken_WhenUserLogsInAgain()
        {
            await AddUserAsync("email@email.com", "name", "password");

            await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "password"), TestIp, CancellationToken.None);
            await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "password"), TestIp, CancellationToken.None);

            Assert.Equal(2, RefreshTokenSingleton.Instance.Count);
            Assert.True(RefreshTokenSingleton.Instance[0].IsRevoked);
            Assert.False(RefreshTokenSingleton.Instance[1].IsRevoked);
        }
    }
}