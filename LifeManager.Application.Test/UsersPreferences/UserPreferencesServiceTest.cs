using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.UsersPreferences.DTOs;
using LifeManager.Application.UsersPreferences.Services;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences.Enums;
using LifeManager.Domain.UsersPreferences.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.UsersPreferences
{
    [Collection("ApplicationServices")]
    public class UserPreferencesServiceTest : BaseTest
    {
        private readonly UserPreferencesService _userPreferencesService;

        public UserPreferencesServiceTest()
        {
            _userPreferencesService = ServiceProvider.GetRequiredService<UserPreferencesService>();

            UserPreferencesSingleton.Instance.Clear();
        }

        [Fact]
        public async Task AddOrUpdateAsync_ShouldAddUserPreferences_WhenUserHasNoPreferences()
        {
            var userPreferencesDto = new UserPreferencesDto(Theme.Dark, Language.EN);

            var result = await _userPreferencesService.AddOrUpdateAsync(userPreferencesDto, new UserId(1), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(userPreferencesDto.Theme, result.Value.Theme);
            Assert.Equal(userPreferencesDto.Language, result.Value.Language);
            var userPreferences = Assert.Single(UserPreferencesSingleton.Instance);
            Assert.Equal(1, userPreferences.Id!.Value);
            Assert.Equal(1, userPreferences.UserId.Value);
            Assert.Equal(userPreferencesDto.Theme, userPreferences.Theme);
            Assert.Equal(userPreferencesDto.Language, userPreferences.Language);
        }

        [Fact]
        public async Task AddOrUpdateAsync_ShouldUpdateUserPreferences_WhenUserAlreadyHasPreferences()
        {
            var userId = new UserId(1);
            await _userPreferencesService.AddOrUpdateAsync(new UserPreferencesDto(Theme.Light, Language.PTBR), userId, CancellationToken.None);
            var addedPreferencesId = UserPreferencesSingleton.Instance.Single().Id;

            var result = await _userPreferencesService.AddOrUpdateAsync(new UserPreferencesDto(Theme.Dark, Language.EN), userId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(Theme.Dark, result.Value.Theme);
            Assert.Equal(Language.EN, result.Value.Language);
            var userPreferences = Assert.Single(UserPreferencesSingleton.Instance);
            Assert.Equal(addedPreferencesId, userPreferences.Id);
            Assert.Equal(Theme.Dark, userPreferences.Theme);
            Assert.Equal(Language.EN, userPreferences.Language);
        }

        [Fact]
        public async Task AddOrUpdateAsync_ShouldOnlyUpdateGivenUserPreferences_WhenOtherUsersHavePreferences()
        {
            await _userPreferencesService.AddOrUpdateAsync(new UserPreferencesDto(Theme.Light, Language.PTBR), new UserId(1), CancellationToken.None);
            await _userPreferencesService.AddOrUpdateAsync(new UserPreferencesDto(Theme.Light, Language.PTBR), new UserId(2), CancellationToken.None);

            await _userPreferencesService.AddOrUpdateAsync(new UserPreferencesDto(Theme.Dark, Language.EN), new UserId(2), CancellationToken.None);

            Assert.Equal(2, UserPreferencesSingleton.Instance.Count);
            var firstUserPreferences = UserPreferencesSingleton.Instance.Single(preferences => preferences.UserId.Value == 1);
            var secondUserPreferences = UserPreferencesSingleton.Instance.Single(preferences => preferences.UserId.Value == 2);
            Assert.Equal(Theme.Light, firstUserPreferences.Theme);
            Assert.Equal(Language.PTBR, firstUserPreferences.Language);
            Assert.Equal(Theme.Dark, secondUserPreferences.Theme);
            Assert.Equal(Language.EN, secondUserPreferences.Language);
        }

        [Fact]
        public async Task AddOrUpdateAsync_ShouldAddPreferencesForEachUser_WhenUsersAreDifferent()
        {
            var firstResult = await _userPreferencesService.AddOrUpdateAsync(new UserPreferencesDto(Theme.Light, Language.EN), new UserId(1), CancellationToken.None);
            var secondResult = await _userPreferencesService.AddOrUpdateAsync(new UserPreferencesDto(Theme.Dark, Language.PTBR), new UserId(2), CancellationToken.None);

            Assert.True(firstResult.IsSuccess);
            Assert.True(secondResult.IsSuccess);
            Assert.Equal(2, UserPreferencesSingleton.Instance.Count);
            Assert.Equal(1, UserPreferencesSingleton.Instance.Single(preferences => preferences.UserId.Value == 1).Id!.Value);
            Assert.Equal(2, UserPreferencesSingleton.Instance.Single(preferences => preferences.UserId.Value == 2).Id!.Value);
        }

        [Fact]
        public async Task AddOrUpdateAsync_ShouldReturnValidationError_WhenUserHasNoPreferencesAndThemeIsInvalid()
        {
            var result = await _userPreferencesService.AddOrUpdateAsync(new UserPreferencesDto((Theme)99, Language.EN), new UserId(1), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserPreferencesErrors.InvalidTheme, result.Error);
            Assert.Equal(ErrorType.Validation, result.Error.Type);
            Assert.Empty(UserPreferencesSingleton.Instance);
        }

        [Fact]
        public async Task AddOrUpdateAsync_ShouldKeepOriginalPreferences_WhenUpdatingWithInvalidLanguage()
        {
            var userId = new UserId(1);
            await _userPreferencesService.AddOrUpdateAsync(new UserPreferencesDto(Theme.Dark, Language.EN), userId, CancellationToken.None);

            var result = await _userPreferencesService.AddOrUpdateAsync(new UserPreferencesDto(Theme.Light, (Language)0), userId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserPreferencesErrors.InvalidLanguage, result.Error);
            var userPreferences = Assert.Single(UserPreferencesSingleton.Instance);
            Assert.Equal(Theme.Dark, userPreferences.Theme);
            Assert.Equal(Language.EN, userPreferences.Language);
        }

        [Fact]
        public async Task GetByUserIdAsync_ShouldReturnPreferences_WhenUserHasPreferences()
        {
            var userId = new UserId(1);
            await _userPreferencesService.AddOrUpdateAsync(new UserPreferencesDto(Theme.Dark, Language.EN), userId, CancellationToken.None);

            var userPreferences = await _userPreferencesService.GetByUserIdAsync(userId, CancellationToken.None);

            Assert.Equal(Theme.Dark, userPreferences.Theme);
            Assert.Equal(Language.EN, userPreferences.Language);
        }

        [Fact]
        public async Task GetByUserIdAsync_ShouldReturnDefaultPreferences_WhenNoPreferencesExist()
        {
            var userPreferences = await _userPreferencesService.GetByUserIdAsync(new UserId(1), CancellationToken.None);

            Assert.Equal(Theme.Light, userPreferences.Theme);
            Assert.Equal(Language.PTBR, userPreferences.Language);
            Assert.Empty(UserPreferencesSingleton.Instance);
        }

        [Fact]
        public async Task GetByUserIdAsync_ShouldReturnDefaultPreferences_WhenOnlyOtherUsersHavePreferences()
        {
            await _userPreferencesService.AddOrUpdateAsync(new UserPreferencesDto(Theme.Dark, Language.EN), new UserId(1), CancellationToken.None);

            var userPreferences = await _userPreferencesService.GetByUserIdAsync(new UserId(2), CancellationToken.None);

            Assert.Equal(Theme.Light, userPreferences.Theme);
            Assert.Equal(Language.PTBR, userPreferences.Language);
            Assert.Single(UserPreferencesSingleton.Instance);
        }

        [Fact]
        public async Task GetByUserIdAsync_ShouldReturnCorrectPreferences_WhenMultipleUsersHavePreferences()
        {
            await _userPreferencesService.AddOrUpdateAsync(new UserPreferencesDto(Theme.Light, Language.EN), new UserId(1), CancellationToken.None);
            await _userPreferencesService.AddOrUpdateAsync(new UserPreferencesDto(Theme.Dark, Language.PTBR), new UserId(2), CancellationToken.None);

            var userPreferences = await _userPreferencesService.GetByUserIdAsync(new UserId(2), CancellationToken.None);

            Assert.Equal(Theme.Dark, userPreferences.Theme);
            Assert.Equal(Language.PTBR, userPreferences.Language);
        }
    }
}
