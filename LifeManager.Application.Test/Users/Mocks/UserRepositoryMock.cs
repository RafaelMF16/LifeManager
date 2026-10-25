using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences;
using LifeManager.Domain.UsersPreferences.Enums;

namespace LifeManager.Application.Test.Users.Mocks
{
    public class UserRepositoryMock : IUserRepository
    {
        private readonly UserSingleton _instance;

        public UserRepositoryMock()
        {
            _instance = UserSingleton.Instance;
        }

        // Mirrors the real repository's transaction: everything is built first and only then stored.
        public Task<User> AddWithStarterDataAsync(User user, Theme? theme, Language? language, CancellationToken cancellationToken)
        {
            var newId = _instance.Count + 1;
            user.AssignId(newId);

            var preferences = UserPreferences.CreateDefault(user.Id!, theme, language);
            var categories = DefaultCategories.CreateFor(user.Id!, preferences.Language);

            _instance.Add(user);

            preferences.AssignId(UserPreferencesSingleton.Instance.Count + 1);
            UserPreferencesSingleton.Instance.Add(preferences);

            var nextCategoryId = CategorySingleton.Instance.Count == 0 ? 1 : CategorySingleton.Instance.Max(storedCategory => storedCategory.Id!.Value) + 1;
            foreach (var category in categories)
            {
                category.AssignId(nextCategoryId++);
                CategorySingleton.Instance.Add(category);
            }

            return Task.FromResult(user);
        }

        public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken)
            => Task.FromResult(_instance.SingleOrDefault(user => user.Id == id));

        public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken)
            => Task.FromResult(_instance.SingleOrDefault(user => user.Email.Value == email.Value));

        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken)
            => Task.FromResult(_instance.Any(user => user.Email.Value == email.Value));
    }
}
