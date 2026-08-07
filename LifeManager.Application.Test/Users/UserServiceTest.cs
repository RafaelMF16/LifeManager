using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Users.DTOs;
using LifeManager.Application.Users.Services;
using LifeManager.Domain.Users.Errors;
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
        public void AddUser_ShouldAddUser_WhenUserIsValid()
        {
            var name = "name";
            var email = "email@email.com";
            var password = "password";
            var userDto = new UserDto(email, name, password);
            var userResponseResult = _userService.AddUser(userDto);

            Assert.NotEmpty(UserSingleton.Instance);
            Assert.All(UserSingleton.Instance, user =>
            {
                Assert.Equal(userResponseResult.Value.Id, user.Id!.Value);
                Assert.Equal(userResponseResult.Value.Name, user.Name.Value);
                Assert.Equal(userResponseResult.Value.Email, user.Email.Value);
            });
        }

        [Fact]
        public void AddUser_ShouldNotAddUser_WhenUserIsInvalid()
        {
            var name = "name";
            var email = "email";
            var password = "password";
            var userDto = new UserDto(email, name, password);

            var result = _userService.AddUser(userDto);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.EmailIsInvalid, result.Error);
            Assert.Empty(UserSingleton.Instance);
        }

        [Fact]
        public void AddUser_ShouldNotAddUser_WhenPasswordIsTooShort()
        {
            var userDto = new UserDto("email@email.com", "name", "short");

            var result = _userService.AddUser(userDto);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.PlainPasswordTooShort, result.Error);
            Assert.Empty(UserSingleton.Instance);
        }

        [Fact]
        public void AddUser_ShouldNotAddUser_WhenPasswordIsTooLong()
        {
            var longPassword = new string('a', 51);
            var userDto = new UserDto("email@email.com", "name", longPassword);

            var result = _userService.AddUser(userDto);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.PlainPasswordTooLong, result.Error);
            Assert.Empty(UserSingleton.Instance);
        }

        [Fact]
        public void AddUser_ShouldNotAddUser_WhenNameIsInvalid()
        {
            var userDto = new UserDto("email@email.com", string.Empty, "password");

            var result = _userService.AddUser(userDto);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.UserNameIsNullOrWhiteSpace, result.Error);
            Assert.Empty(UserSingleton.Instance);
        }

        [Fact]
        public void AddUser_ShouldNotAddUser_WhenEmailAlreadyExists()
        {
            var name = "name";
            var email = "email@email.com";
            var password = "password";
            var userDto = new UserDto(email, name, password);
            _userService.AddUser(userDto);

            var result = _userService.AddUser(userDto);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.EmailRegistered, result.Error);
            Assert.Single(UserSingleton.Instance);
        }

        [Fact]
        public void AuthenticateUser_ShouldReturnFailure_WhenEmailDoesNotExist()
        {
            var result = _userService.AuthenticateUser("missing@email.com", "password");

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.InvalidCredentials, result.Error);
        }

        [Fact]
        public void AuthenticateUser_ShouldReturnFailure_WhenPasswordIsIncorrect()
        {
            var userDto = new UserDto("email@email.com", "name", "password");
            _userService.AddUser(userDto);

            var result = _userService.AuthenticateUser(userDto.Email, "wrongPassword");

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.InvalidCredentials, result.Error);
        }

        [Fact]
        public void AuthenticateUser_ShouldReturnTokens_WhenCredentialsAreValid()
        {
            var userDto = new UserDto("email@email.com", "name", "password");
            _userService.AddUser(userDto);

            var result = _userService.AuthenticateUser(userDto.Email, userDto.UserPassword);

            Assert.True(result.IsSuccess);
            Assert.False(string.IsNullOrWhiteSpace(result.Value.AccessToken));
            Assert.False(string.IsNullOrWhiteSpace(result.Value.RefreshToken));
        }

        [Fact]
        public void AuthenticateUser_ShouldRevokePreviousToken_WhenUserLogsInAgain()
        {
            var userDto = new UserDto("email@email.com", "name", "password");
            _userService.AddUser(userDto);

            _userService.AuthenticateUser(userDto.Email, userDto.UserPassword);
            _userService.AuthenticateUser(userDto.Email, userDto.UserPassword);

            Assert.Equal(2, RefreshTokenSingleton.Instance.Count);
            Assert.True(RefreshTokenSingleton.Instance[0].IsRevoked);
            Assert.False(RefreshTokenSingleton.Instance[1].IsRevoked);
        }
    }
}