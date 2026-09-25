using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.UsersPreferences.Interfaces
{
    public interface IUserPreferencesRepository
    {
        UserPreferences Add(UserPreferences userPreferences);
        UserPreferences? GetUserPreferencesByUserId(UserId userId);
        UserPreferences Update(UserPreferences userPreferences);
    }
}