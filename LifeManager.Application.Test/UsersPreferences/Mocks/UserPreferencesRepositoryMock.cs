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

        public Task<UserPreferences> AddAsync(UserPreferences userPreferences, CancellationToken cancellationToken)
        {
            _instance.Add(userPreferences);

            var newId = _instance.Count;
            userPreferences.AssignId(newId);

            return Task.FromResult(userPreferences);
        }

        public Task UpdateAsync(UserPreferences userPreferences, CancellationToken cancellationToken)
        {
            var index = _instance.FindIndex(storedPreferences => storedPreferences.Id == userPreferences.Id && storedPreferences.UserId == userPreferences.UserId);
            if (index >= 0)
                _instance[index] = ToDetachedCopy(userPreferences);

            return Task.CompletedTask;
        }

        public Task<UserPreferences?> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken)
        {
            var storedPreferences = _instance.SingleOrDefault(userPreferences => userPreferences.UserId == userId);

            // Returns a detached copy, like AsNoTracking in the real repository, so changes only persist through UpdateAsync
            return Task.FromResult(storedPreferences is null ? null : ToDetachedCopy(storedPreferences));
        }

        private static UserPreferences ToDetachedCopy(UserPreferences userPreferences)
        {
            var copy = UserPreferences.Create(userPreferences.UserId.Value, userPreferences.Theme, userPreferences.Language).Value!;
            copy.AssignId(userPreferences.Id!.Value);

            return copy;
        }
    }
}
