using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Auth.Services;
using LifeManager.Application.Test.Users.Mocks;
using LifeManager.Application.Users.DTOs;
using LifeManager.Application.Users.Services;
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
        }

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
            var result = await _userService.AuthenticateUserAsync(new LoginDto("missing@email.com", "password"), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.InvalidCredentials, result.Error);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldReturnFailure_WhenPasswordIsIncorrect()
        {
            await AddUserAsync("email@email.com", "name", "password");

            var result = await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "wrongPassword"), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.InvalidCredentials, result.Error);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldReturnTokens_WhenCredentialsAreValid()
        {
            await AddUserAsync("email@email.com", "name", "password");

            var result = await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "password"), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.False(string.IsNullOrWhiteSpace(result.Value.AccessToken));
            Assert.False(string.IsNullOrWhiteSpace(result.Value.RefreshToken));
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldRevokePreviousToken_WhenUserLogsInAgain()
        {
            await AddUserAsync("email@email.com", "name", "password");

            await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "password"), CancellationToken.None);
            await _userService.AuthenticateUserAsync(new LoginDto("email@email.com", "password"), CancellationToken.None);

            Assert.Equal(2, RefreshTokenSingleton.Instance.Count);
            Assert.True(RefreshTokenSingleton.Instance[0].IsRevoked);
            Assert.False(RefreshTokenSingleton.Instance[1].IsRevoked);
        }
    }
}