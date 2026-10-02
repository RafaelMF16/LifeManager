using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Users.DTOs;
using LifeManager.Application.Users.Services;
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
        }

        [Fact]
        public async Task AddUserAsync_ShouldAddUser_WhenUserIsValid()
        {
            var name = "name";
            var email = "email@email.com";
            var password = "password";
            var userDto = new UserDto(email, name, password);
            var userResponseResult = await _userService.AddUserAsync(userDto, CancellationToken.None);

            Assert.NotEmpty(UserSingleton.Instance);
            Assert.All(UserSingleton.Instance, user =>
            {
                Assert.Equal(userResponseResult.Value.Id, user.Id!.Value);
                Assert.Equal(userResponseResult.Value.Name, user.Name.Value);
                Assert.Equal(userResponseResult.Value.Email, user.Email.Value);
            });
        }

        [Fact]
        public async Task AddUserAsync_ShouldNotAddUser_WhenUserIsInvalid()
        {
            var name = "name";
            var email = "email";
            var password = "password";
            var userDto = new UserDto(email, name, password);

            var result = await _userService.AddUserAsync(userDto, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.EmailIsInvalid, result.Error);
            Assert.Empty(UserSingleton.Instance);
        }

        [Fact]
        public async Task AddUserAsync_ShouldNotAddUser_WhenPasswordIsTooShort()
        {
            var userDto = new UserDto("email@email.com", "name", "short");

            var result = await _userService.AddUserAsync(userDto, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.PlainPasswordTooShort, result.Error);
            Assert.Empty(UserSingleton.Instance);
        }

        [Fact]
        public async Task AddUserAsync_ShouldNotAddUser_WhenPasswordIsTooLong()
        {
            var longPassword = new string('a', 51);
            var userDto = new UserDto("email@email.com", "name", longPassword);

            var result = await _userService.AddUserAsync(userDto, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.PlainPasswordTooLong, result.Error);
            Assert.Empty(UserSingleton.Instance);
        }

        [Fact]
        public async Task AddUserAsync_ShouldNotAddUser_WhenNameIsInvalid()
        {
            var userDto = new UserDto("email@email.com", string.Empty, "password");

            var result = await _userService.AddUserAsync(userDto, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.UserNameIsNullOrWhiteSpace, result.Error);
            Assert.Empty(UserSingleton.Instance);
        }

        [Fact]
        public async Task AddUserAsync_ShouldNotAddUser_WhenEmailAlreadyExists()
        {
            var name = "name";
            var email = "email@email.com";
            var password = "password";
            var userDto = new UserDto(email, name, password);
            await _userService.AddUserAsync(userDto, CancellationToken.None);

            var result = await _userService.AddUserAsync(userDto, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.EmailRegistered, result.Error);
            Assert.Single(UserSingleton.Instance);
        }

        [Fact]
        public async Task GetCurrentUserAsync_ShouldReturnName_WhenUserExists()
        {
            var userResponse = (await _userService.AddUserAsync(new UserDto("email@email.com", "name", "password"), CancellationToken.None)).Value;

            var result = await _userService.GetCurrentUserAsync(new UserId(userResponse.Id), CancellationToken.None);

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
            var userDto = new UserDto("email@email.com", "name", "password");
            await _userService.AddUserAsync(userDto, CancellationToken.None);

            var result = await _userService.AuthenticateUserAsync(new LoginDto(userDto.Email, "wrongPassword"), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.InvalidCredentials, result.Error);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldReturnTokens_WhenCredentialsAreValid()
        {
            var userDto = new UserDto("email@email.com", "name", "password");
            await _userService.AddUserAsync(userDto, CancellationToken.None);

            var result = await _userService.AuthenticateUserAsync(new LoginDto(userDto.Email, userDto.UserPassword), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.False(string.IsNullOrWhiteSpace(result.Value.AccessToken));
            Assert.False(string.IsNullOrWhiteSpace(result.Value.RefreshToken));
        }

        [Fact]
        public async Task AuthenticateUserAsync_ShouldRevokePreviousToken_WhenUserLogsInAgain()
        {
            var userDto = new UserDto("email@email.com", "name", "password");
            await _userService.AddUserAsync(userDto, CancellationToken.None);

            await _userService.AuthenticateUserAsync(new LoginDto(userDto.Email, userDto.UserPassword), CancellationToken.None);
            await _userService.AuthenticateUserAsync(new LoginDto(userDto.Email, userDto.UserPassword), CancellationToken.None);

            Assert.Equal(2, RefreshTokenSingleton.Instance.Count);
            Assert.True(RefreshTokenSingleton.Instance[0].IsRevoked);
            Assert.False(RefreshTokenSingleton.Instance[1].IsRevoked);
        }
    }
}