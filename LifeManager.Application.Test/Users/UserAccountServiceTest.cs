using LifeManager.Application.Auth.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.Users.Mocks;
using LifeManager.Application.Users.DTOs;
using LifeManager.Application.Users.Services;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Errors;
using LifeManager.Domain.Users.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Users
{
    [Collection("ApplicationServices")]
    public class UserAccountServiceTest : BaseTest
    {
        private const string Password = "password1";

        private readonly UserAccountService _service;
        private readonly FakeTimeProvider _clock;

        public UserAccountServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<UserAccountService>();
            _clock = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();

            UserSingleton.Instance.Clear();
            RefreshTokenSingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();
            UserPreferencesSingleton.Instance.Clear();
        }

        private AuthService Auth => ServiceProvider.GetRequiredService<AuthService>();

        private async Task<User> AddUserAsync(string email)
        {
            var user = User.Create("name", email, Auth.EncryptPassword(Password), _clock.UtcNow).Value!;
            return await ((UserRepositoryMock)ServiceProvider.GetRequiredService<IUserRepository>())
                .AddWithStarterDataAsync(user, null, null, CancellationToken.None);
        }

        /// <summary>A session of the user: the raw cookie value and its stored token.</summary>
        private (string Cookie, RefreshToken Token) AddSession(User user, string cookie)
        {
            var hash = ServiceProvider.GetRequiredService<TokenService>().HashOf(cookie).Value!;
            var token = RefreshToken.Create(user.Id!.Value, hash.Value, _clock.UtcNow, _clock.UtcNow.AddDays(7), _clock.UtcNow.AddDays(30), false).Value!;
            RefreshTokenSingleton.Instance.Add(token);
            return (cookie, token);
        }

        private User Stored(User user) => UserSingleton.Instance.Single(stored => stored.Id == user.Id);

        [Fact]
        public async Task ChangePasswordAsync_ShouldStoreTheNewPasswordAndKeepOnlyTheCurrentSession()
        {
            var user = await AddUserAsync("ana@email.com");
            var current = AddSession(user, "current-cookie");
            var other = AddSession(user, "other-cookie");
            var someoneElse = AddSession(await AddUserAsync("bia@email.com"), "bia-cookie");

            var result = await _service.ChangePasswordAsync(user.Id!, new ChangePasswordDto(Password, "newPassword1"), current.Cookie, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(Auth.VerifyPassword("newPassword1", Stored(user).PasswordHash.Value));
            Assert.False(current.Token.IsRevoked);
            Assert.True(other.Token.IsRevoked);
            Assert.False(someoneElse.Token.IsRevoked);
        }

        [Fact]
        public async Task ChangePasswordAsync_ShouldEndEverySession_WhenThereIsNoCookie()
        {
            var user = await AddUserAsync("ana@email.com");
            var session = AddSession(user, "a-cookie");

            var result = await _service.ChangePasswordAsync(user.Id!, new ChangePasswordDto(Password, "newPassword1"), null, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(session.Token.IsRevoked);
        }

        [Fact]
        public async Task ChangePasswordAsync_ShouldChangeNothing_WhenTheCurrentPasswordIsWrong()
        {
            var user = await AddUserAsync("ana@email.com");
            var hashBefore = Stored(user).PasswordHash.Value;
            var other = AddSession(user, "other-cookie");

            // The wrong password is reported first, even with an invalid new one.
            var result = await _service.ChangePasswordAsync(user.Id!, new ChangePasswordDto("wrongPassword", "short"), null, CancellationToken.None);

            Assert.Equal(UserErrors.WrongPassword, result.Error);
            Assert.Equal(hashBefore, Stored(user).PasswordHash.Value);
            Assert.False(other.Token.IsRevoked);
        }

        [Theory]
        [InlineData("short", "User.PlainPasswordTooShort")]
        [InlineData("", "User.PlainPasswordIsNullOrWhiteSpace")]
        public async Task ChangePasswordAsync_ShouldChangeNothing_WhenTheNewPasswordIsInvalid(string newPassword, string code)
        {
            var user = await AddUserAsync("ana@email.com");
            var hashBefore = Stored(user).PasswordHash.Value;

            var result = await _service.ChangePasswordAsync(user.Id!, new ChangePasswordDto(Password, newPassword), null, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(code, result.Error.Code);
            Assert.Equal(hashBefore, Stored(user).PasswordHash.Value);
        }

        [Fact]
        public async Task LogoutAllAsync_ShouldEndEverySessionOfTheUserOnly()
        {
            var user = await AddUserAsync("ana@email.com");
            var first = AddSession(user, "first-cookie");
            var second = AddSession(user, "second-cookie");
            var someoneElse = AddSession(await AddUserAsync("bia@email.com"), "bia-cookie");

            var result = await _service.LogoutAllAsync(user.Id!, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(first.Token.IsRevoked);
            Assert.True(second.Token.IsRevoked);
            Assert.False(someoneElse.Token.IsRevoked);
        }
    }
}
