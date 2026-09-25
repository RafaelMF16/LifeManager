using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences;
using LifeManager.Domain.UsersPreferences.Enums;
using LifeManager.Domain.UsersPreferences.Errors;

namespace LifeManager.Domain.Test.UsersPreferences
{
    public class UserPreferencesTests
    {
        [Fact]
        public void Create_ShouldReturnUserPreferences_WhenValuesAreValid()
        {
            var userId = 1;
            var theme = Theme.Dark;
            var language = Language.PTBR;
            var userPreferences = UserPreferences.Create(userId, theme, language).Value;

            Assert.NotNull(userPreferences);
            Assert.IsType<UserPreferences>(userPreferences);
            Assert.Null(userPreferences.Id);
            Assert.Equal(userId, userPreferences.UserId.Value);
            Assert.Equal(theme, userPreferences.Theme);
            Assert.Equal(language, userPreferences.Language);
        }

        [Fact]
        public void AssignId_ShouldSetId_WhenUserPreferencesHasNoIdYet()
        {
            var userPreferences = UserPreferences.Create(1, Theme.Light, Language.EN).Value!;

            userPreferences.AssignId(10);

            Assert.NotNull(userPreferences.Id);
            Assert.Equal(10, userPreferences.Id!.Value);
        }

        [Fact]
        public void Update_ShouldChangeThemeAndLanguage_WhenValuesAreValid()
        {
            var userPreferences = UserPreferences.Create(1, Theme.Light, Language.EN).Value!;

            var result = userPreferences.Update(Theme.Dark, Language.PTBR);

            Assert.True(result.IsSuccess);
            Assert.Same(userPreferences, result.Value);
            Assert.Equal(Theme.Dark, userPreferences.Theme);
            Assert.Equal(Language.PTBR, userPreferences.Language);
        }

        [Fact]
        public void Update_ShouldReturnInvalidTheme_WhenThemeIsNotDefined()
        {
            var userPreferences = UserPreferences.Create(1, Theme.Light, Language.EN).Value!;

            var result = userPreferences.Update((Theme)99, Language.PTBR);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserPreferencesErrors.InvalidTheme, result.Error);
            Assert.Equal(Theme.Light, userPreferences.Theme);
            Assert.Equal(Language.EN, userPreferences.Language);
        }

        [Fact]
        public void Update_ShouldReturnInvalidLanguage_WhenLanguageIsNotDefined()
        {
            var userPreferences = UserPreferences.Create(1, Theme.Light, Language.EN).Value!;

            var result = userPreferences.Update(Theme.Dark, (Language)0);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserPreferencesErrors.InvalidLanguage, result.Error);
            Assert.Equal(Theme.Light, userPreferences.Theme);
            Assert.Equal(Language.EN, userPreferences.Language);
        }

        [Fact]
        public void Create_ShouldReturnInvalidTheme_WhenThemeIsNotDefined()
        {
            var result = UserPreferences.Create(1, (Theme)0, Language.EN);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserPreferencesErrors.InvalidTheme, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnInvalidLanguage_WhenLanguageIsNotDefined()
        {
            var result = UserPreferences.Create(1, Theme.Dark, (Language)99);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserPreferencesErrors.InvalidLanguage, result.Error);
        }

        [Fact]
        public void CreateDefault_ShouldReturnLightThemeAndPtBr_WhenCalled()
        {
            var userId = new UserId(1);

            var userPreferences = UserPreferences.CreateDefault(userId);

            Assert.Null(userPreferences.Id);
            Assert.Equal(userId, userPreferences.UserId);
            Assert.Equal(Theme.Light, userPreferences.Theme);
            Assert.Equal(Language.PTBR, userPreferences.Language);
        }

        [Fact]
        public void Equals_ShouldReturnTrue_WhenUserPreferencesHaveTheSameId()
        {
            var userPreferences1 = UserPreferences.Create(1, Theme.Light, Language.EN).Value!;
            var userPreferences2 = UserPreferences.Create(2, Theme.Dark, Language.PTBR).Value!;
            userPreferences1.AssignId(1);
            userPreferences2.AssignId(1);

            Assert.Equal(userPreferences1, userPreferences2);
            Assert.Equal(userPreferences1.GetHashCode(), userPreferences2.GetHashCode());
        }

        [Fact]
        public void Equals_ShouldReturnFalse_WhenUserPreferencesHaveDifferentIds()
        {
            var userPreferences1 = UserPreferences.Create(1, Theme.Light, Language.EN).Value!;
            var userPreferences2 = UserPreferences.Create(1, Theme.Light, Language.EN).Value!;
            userPreferences1.AssignId(1);
            userPreferences2.AssignId(2);

            Assert.NotEqual(userPreferences1, userPreferences2);
        }

        [Fact]
        public void Equals_ShouldReturnFalse_WhenNeitherUserPreferencesHasBeenAssignedAnId()
        {
            var userPreferences1 = UserPreferences.Create(1, Theme.Light, Language.EN).Value!;
            var userPreferences2 = UserPreferences.Create(1, Theme.Light, Language.EN).Value!;

            Assert.NotEqual(userPreferences1, userPreferences2);
            Assert.Equal(userPreferences1, userPreferences1);
        }
    }
}
