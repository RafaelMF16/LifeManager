using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Users.DTOs;
using LifeManager.Application.Users.Services;
using LifeManager.Domain.Exceptions;
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
        public void Add_ShouldAddUser_WhenUserIsValid()
        {
            var name = "name";
            var email = "email@email.com";
            var password = "password";
            var userDto = new UserDto(email, name, password);
            var newUser = _userService.AddUser(userDto);

            Assert.NotEmpty(UserSingleton.Instance);
            Assert.All(UserSingleton.Instance, user =>
            {
                Assert.Equal(newUser.Id, user.Id!.Value);
                Assert.Equal(newUser.Name, user.Name.Value);
                Assert.Equal(newUser.Email, user.Email.Value);
            });
        }

        [Fact]
        public void Add_ShouldNotAddUser_WhenUserIsInvalid()
        {
            var name = "name";
            var email = "email";
            var password = "password";
            var userDto = new UserDto(email, name, password);

            Assert.Throws<DomainException>(() => _userService.AddUser(userDto));
            Assert.Empty(UserSingleton.Instance);
        }

        [Fact]
        public void Add_ShouldNotAddUser_WhenEmailAlreadyExists()
        {
            var name = "name";
            var email = "email@email.com";
            var password = "password";
            var userDto = new UserDto(email, name, password);
            _userService.AddUser(userDto);

            Assert.Throws<DomainException>(() => _userService.AddUser(userDto));
            Assert.Single(UserSingleton.Instance);
        }

        [Fact]
        public void AuthenticateUser_ShouldReturnNull_WhenEmailDoesNotExist()
        {
            var result = _userService.AuthenticateUser("missing@email.com", "password");

            Assert.Null(result);
        }

        [Fact]
        public void AuthenticateUser_ShouldReturnNull_WhenPasswordIsIncorrect()
        {
            var userDto = new UserDto("email@email.com", "name", "password");
            _userService.AddUser(userDto);

            var result = _userService.AuthenticateUser(userDto.Email, "wrongPassword");

            Assert.Null(result);
        }

        [Fact]
        public void AuthenticateUser_ShouldReturnTokens_WhenCredentialsAreValid()
        {
            var userDto = new UserDto("email@email.com", "name", "password");
            _userService.AddUser(userDto);

            var result = _userService.AuthenticateUser(userDto.Email, userDto.UserPassword);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result!.AccessToken));
            Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        }

        [Fact]
        public void AuthenticateUser_ShouldRevokePreviousToken_WhenUserLogsInAgain()
        {
            var userDto = new UserDto("email@email.com", "name", "password");
            _userService.AddUser(userDto);

            _userService.AuthenticateUser(userDto.Email, userDto.UserPassword);
            _userService.AuthenticateUser(userDto.Email, userDto.UserPassword);

            Assert.Equal(2, RefreshTokenSingleton.Instance.Count);
            Assert.True(RefreshTokenSingleton.Instance[0].IsRevoked.Value);
            Assert.False(RefreshTokenSingleton.Instance[1].IsRevoked.Value);
        }
    }
}