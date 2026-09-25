using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.UsersPreferences.DTOs;
using LifeManager.Application.UsersPreferences.Services;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences.Enums;
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
        public void AddOrUpdate_ShouldAddUserPreferences_WhenUserHasNoPreferences()
        {
            var userPreferencesDto = new UserPreferencesDto(Theme.Dark, Language.EN);

            var result = _userPreferencesService.AddOrUpdate(userPreferencesDto, new UserId(1));

            Assert.True(result.IsSuccess);
            var userPreferences = Assert.Single(UserPreferencesSingleton.Instance);
            Assert.Equal(1, userPreferences.Id!.Value);
            Assert.Equal(1, userPreferences.UserId.Value);
            Assert.Equal(userPreferencesDto.Theme, userPreferences.Theme);
            Assert.Equal(userPreferencesDto.Language, userPreferences.Language);
            Assert.Same(userPreferences, result.Value);
        }

        [Fact]
        public void AddOrUpdate_ShouldUpdateUserPreferences_WhenUserAlreadyHasPreferences()
        {
            var userId = new UserId(1);
            var addedPreferences = _userPreferencesService.AddOrUpdate(new UserPreferencesDto(Theme.Light, Language.PTBR), userId).Value!;

            var result = _userPreferencesService.AddOrUpdate(new UserPreferencesDto(Theme.Dark, Language.EN), userId);

            Assert.True(result.IsSuccess);
            var userPreferences = Assert.Single(UserPreferencesSingleton.Instance);
            Assert.Equal(addedPreferences.Id, userPreferences.Id);
            Assert.Equal(Theme.Dark, userPreferences.Theme);
            Assert.Equal(Language.EN, userPreferences.Language);
            Assert.Equal(addedPreferences.Id, result.Value.Id);
        }

        [Fact]
        public void AddOrUpdate_ShouldOnlyUpdateGivenUserPreferences_WhenOtherUsersHavePreferences()
        {
            _userPreferencesService.AddOrUpdate(new UserPreferencesDto(Theme.Light, Language.PTBR), new UserId(1));
            _userPreferencesService.AddOrUpdate(new UserPreferencesDto(Theme.Light, Language.PTBR), new UserId(2));

            _userPreferencesService.AddOrUpdate(new UserPreferencesDto(Theme.Dark, Language.EN), new UserId(2));

            Assert.Equal(2, UserPreferencesSingleton.Instance.Count);
            var firstUserPreferences = UserPreferencesSingleton.Instance.Single(preferences => preferences.UserId.Value == 1);
            var secondUserPreferences = UserPreferencesSingleton.Instance.Single(preferences => preferences.UserId.Value == 2);
            Assert.Equal(Theme.Light, firstUserPreferences.Theme);
            Assert.Equal(Language.PTBR, firstUserPreferences.Language);
            Assert.Equal(Theme.Dark, secondUserPreferences.Theme);
            Assert.Equal(Language.EN, secondUserPreferences.Language);
        }

        [Fact]
        public void AddOrUpdate_ShouldAddPreferencesForEachUser_WhenUsersAreDifferent()
        {
            var firstResult = _userPreferencesService.AddOrUpdate(new UserPreferencesDto(Theme.Light, Language.EN), new UserId(1));
            var secondResult = _userPreferencesService.AddOrUpdate(new UserPreferencesDto(Theme.Dark, Language.PTBR), new UserId(2));

            Assert.True(firstResult.IsSuccess);
            Assert.True(secondResult.IsSuccess);
            Assert.Equal(2, UserPreferencesSingleton.Instance.Count);
            Assert.Equal(1, firstResult.Value.Id!.Value);
            Assert.Equal(2, secondResult.Value.Id!.Value);
        }

        [Fact]
        public void GetUserPreferencesByUserId_ShouldReturnPreferences_WhenUserHasPreferences()
        {
            var userId = new UserId(1);
            _userPreferencesService.AddOrUpdate(new UserPreferencesDto(Theme.Dark, Language.EN), userId);

            var userPreferences = _userPreferencesService.GetUserPreferencesByUserId(userId);

            Assert.Equal(userId, userPreferences.UserId);
            Assert.Equal(Theme.Dark, userPreferences.Theme);
            Assert.Equal(Language.EN, userPreferences.Language);
        }

        [Fact]
        public void GetUserPreferencesByUserId_ShouldReturnDefaultPreferences_WhenNoPreferencesExist()
        {
            var userPreferences = _userPreferencesService.GetUserPreferencesByUserId(new UserId(1));

            Assert.Null(userPreferences.Id);
            Assert.Equal(1, userPreferences.UserId.Value);
            Assert.Equal(Theme.Light, userPreferences.Theme);
            Assert.Equal(Language.PTBR, userPreferences.Language);
            Assert.Empty(UserPreferencesSingleton.Instance);
        }

        [Fact]
        public void GetUserPreferencesByUserId_ShouldReturnDefaultPreferences_WhenOnlyOtherUsersHavePreferences()
        {
            _userPreferencesService.AddOrUpdate(new UserPreferencesDto(Theme.Dark, Language.EN), new UserId(1));

            var userPreferences = _userPreferencesService.GetUserPreferencesByUserId(new UserId(2));

            Assert.Null(userPreferences.Id);
            Assert.Equal(2, userPreferences.UserId.Value);
            Assert.Equal(Theme.Light, userPreferences.Theme);
            Assert.Equal(Language.PTBR, userPreferences.Language);
            Assert.Single(UserPreferencesSingleton.Instance);
        }

        [Fact]
        public void GetUserPreferencesByUserId_ShouldReturnCorrectPreferences_WhenMultipleUsersHavePreferences()
        {
            _userPreferencesService.AddOrUpdate(new UserPreferencesDto(Theme.Light, Language.EN), new UserId(1));
            _userPreferencesService.AddOrUpdate(new UserPreferencesDto(Theme.Dark, Language.PTBR), new UserId(2));

            var userPreferences = _userPreferencesService.GetUserPreferencesByUserId(new UserId(2));

            Assert.Equal(2, userPreferences.UserId.Value);
            Assert.Equal(Theme.Dark, userPreferences.Theme);
            Assert.Equal(Language.PTBR, userPreferences.Language);
        }
    }
}
