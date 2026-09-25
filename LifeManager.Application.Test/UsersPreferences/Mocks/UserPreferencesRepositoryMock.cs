using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences;
using LifeManager.Domain.UsersPreferences.Interfaces;

namespace LifeManager.Application.Test.UsersPreferences.Mocks
{
    public class UserPreferencesRepositoryMock : IUserPreferencesRepository
    {
        private readonly UserPreferencesSingleton _instance;

        public UserPreferencesRepositoryMock()
        {
            _instance = UserPreferencesSingleton.Instance;
        }

        public UserPreferences Add(UserPreferences userPreferences)
        {
            _instance.Add(userPreferences);

            var newId = _instance.Count;
            userPreferences.AssignId(newId);

            return userPreferences;
        }

        public UserPreferences Update(UserPreferences userPreferences)
        {
            var index = _instance.FindIndex(storedPreferences => storedPreferences.Id == userPreferences.Id);
            _instance[index] = userPreferences;

            return userPreferences;
        }

        public UserPreferences? GetUserPreferencesByUserId(UserId userId)
        {
            var storedPreferences = _instance.FirstOrDefault(userPreferences => userPreferences.UserId == userId);
            if (storedPreferences is null)
                return null;

            // Returns a detached copy, like AsNoTracking in the real repository, so changes only persist through Update
            var userPreferences = UserPreferences.Create(storedPreferences.UserId.Value, storedPreferences.Theme, storedPreferences.Language).Value!;
            userPreferences.AssignId(storedPreferences.Id!.Value);

            return userPreferences;
        }
    }
}
