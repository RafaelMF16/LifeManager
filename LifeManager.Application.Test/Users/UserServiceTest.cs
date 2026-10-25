using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Users.DTOs;
using LifeManager.Application.Users.Services;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Users.Errors;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences.Enums;
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
            Assert.Equal(DefaultCategories.Names(Language.PTBR).Count, CategorySingleton.Instance.Count);
            Assert.Single(UserPreferencesSingleton.Instance);
        }

        [Fact]
        public async Task AddUserAsync_ShouldCreateDefaultCategoriesAndPreferencesInTheChosenLanguageAndTheme_WhenGiven()
        {
            var userDto = new UserDto("email@email.com", "name", "password", Language.EN, Theme.Dark);

            var result = await _userService.AddUserAsync(userDto, CancellationToken.None);

            var userId = new UserId(result.Value!.Id);
            Assert.Equal(
                DefaultCategories.Names(Language.EN),
                CategorySingleton.Instance.Where(category => category.UserId == userId).Select(category => category.Name.Value));
            var preferences = Assert.Single(UserPreferencesSingleton.Instance);
            Assert.Equal(userId, preferences.UserId);
            Assert.Equal(Language.EN, preferences.Language);
            Assert.Equal(Theme.Dark, preferences.Theme);
        }

        [Fact]
        public async Task AddUserAsync_ShouldCreatePortugueseCategoriesAndLightTheme_WhenLanguageAndThemeAreMissing()
        {
            var result = await _userService.AddUserAsync(new UserDto("email@email.com", "name", "password"), CancellationToken.None);

            var userId = new UserId(result.Value!.Id);
            Assert.Equal(
                DefaultCategories.Names(Language.PTBR),
                CategorySingleton.Instance.Where(category => category.UserId == userId).Select(category => category.Name.Value));
            var preferences = Assert.Single(UserPreferencesSingleton.Instance);
            Assert.Equal(Language.PTBR, preferences.Language);
            Assert.Equal(Theme.Light, preferences.Theme);
        }

        [Fact]
        public async Task AddUserAsync_ShouldFallBackToPortugueseAndLightTheme_WhenLanguageAndThemeAreUndefined()
        {
            var userDto = new UserDto("email@email.com", "name", "password", (Language)99, (Theme)99);

            await _userService.AddUserAsync(userDto, CancellationToken.None);

            Assert.Equal(DefaultCategories.Names(Language.PTBR), CategorySingleton.Instance.Select(category => category.Name.Value));
            var preferences = Assert.Single(UserPreferencesSingleton.Instance);
            Assert.Equal(Language.PTBR, preferences.Language);
            Assert.Equal(Theme.Light, preferences.Theme);
        }

        [Fact]
        public async Task AddUserAsync_ShouldNotCreateCategoriesOrPreferences_WhenUserIsInvalid()
        {
            var result = await _userService.AddUserAsync(new UserDto("email", "name", "password", Language.EN, Theme.Dark), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Empty(CategorySingleton.Instance);
            Assert.Empty(UserPreferencesSingleton.Instance);
        }

        [Fact]
        public async Task AddUserAsync_ShouldKeepEachUsersOwnCategoriesAndPreferences_WhenTwoUsersSignUp()
        {
            var first = await _userService.AddUserAsync(new UserDto("first@email.com", "first", "password", Language.EN, Theme.Dark), CancellationToken.None);
            var second = await _userService.AddUserAsync(new UserDto("second@email.com", "second", "password"), CancellationToken.None);

            var firstUserId = new UserId(first.Value!.Id);
            var secondUserId = new UserId(second.Value!.Id);
            Assert.Equal(
                DefaultCategories.Names(Language.EN),
                CategorySingleton.Instance.Where(category => category.UserId == firstUserId).Select(category => category.Name.Value));
            Assert.Equal(
                DefaultCategories.Names(Language.PTBR),
                CategorySingleton.Instance.Where(category => category.UserId == secondUserId).Select(category => category.Name.Value));
            Assert.Equal(Language.EN, UserPreferencesSingleton.Instance.Single(preferences => preferences.UserId == firstUserId).Language);
            Assert.Equal(Language.PTBR, UserPreferencesSingleton.Instance.Single(preferences => preferences.UserId == secondUserId).Language);
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