using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences.Enums;
using LifeManager.Domain.UsersPreferences.Errors;
using LifeManager.Domain.UsersPreferences.ValueObjects;

namespace LifeManager.Domain.UsersPreferences
{
    public class UserPreferences
    {
        public UserPreferencesId? Id { get; private set; }
        public UserId UserId { get; }
        public Theme Theme { get; private set; }
        public Language Language { get; private set; }

        private UserPreferences(
            UserId userId,
            Theme theme,
            Language language)
        {
            UserId = userId;
            Theme = theme;
            Language = language;
        }

        public static Result<UserPreferences> Create(
            int idUser,
            Theme theme,
            Language language)
        {
            var validationError = Validate(theme, language);
            if (validationError is not null)
                return validationError;

            var userId = new UserId(idUser);
            return new UserPreferences(userId, theme, language);
        }

        /// <summary>
        /// The preferences of a user who hasn't saved any. A sign-up passes the theme and language its screen was in;
        /// a missing or undefined value falls back to <see cref="Theme.Light"/> / <see cref="Language.PTBR"/>.
        /// </summary>
        public static UserPreferences CreateDefault(UserId userId, Theme? theme = null, Language? language = null)
        {
            var resolvedTheme = theme is { } givenTheme && Enum.IsDefined(givenTheme) ? givenTheme : Theme.Light;
            var resolvedLanguage = language is { } givenLanguage && Enum.IsDefined(givenLanguage) ? givenLanguage : Language.PTBR;

            return new UserPreferences(userId, resolvedTheme, resolvedLanguage);
        }

        public Result<UserPreferences> Update(Theme theme, Language language)
        {
            var validationError = Validate(theme, language);
            if (validationError is not null)
                return validationError;

            Theme = theme;
            Language = language;

            return this;
        }

        public void AssignId(int id)
        {
            Id = new UserPreferencesId(id);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not UserPreferences other)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Id is null || other.Id is null)
                return false;

            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
            => Id?.GetHashCode() ?? base.GetHashCode();

        private static Error? Validate(Theme theme, Language language)
        {
            if (!Enum.IsDefined(theme))
                return UserPreferencesErrors.InvalidTheme;

            if (!Enum.IsDefined(language))
                return UserPreferencesErrors.InvalidLanguage;

            return null;
        }
    }
}