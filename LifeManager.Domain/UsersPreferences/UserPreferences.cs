using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences.Enums;
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
            var userId = new UserId(idUser);
            return new UserPreferences(userId, theme, language);
        }

        public void Update(Theme theme, Language language)
        {
            Theme = theme;
            Language = language;
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
    }
}