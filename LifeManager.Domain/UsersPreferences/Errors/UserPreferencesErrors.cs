using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.UsersPreferences.Errors
{
    public static class UserPreferencesErrors
    {
        public static readonly Error InvalidTheme = Error.Validation("UserPreferences.InvalidTheme", "Theme is invalid");
        public static readonly Error InvalidLanguage = Error.Validation("UserPreferences.InvalidLanguage", "Language is invalid");
    }
}
