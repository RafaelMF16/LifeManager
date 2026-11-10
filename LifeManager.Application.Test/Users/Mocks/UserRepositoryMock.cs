using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Auth.ValueObjects;
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

        // Test seeding, mirroring how a completed sign-up stores an account (PendingRegistrationRepository.CompleteAsync):
        // the user, their preferences and default categories, all built first and only then stored.
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

        public Task UpdateNameAsync(UserId id, UserName name, CancellationToken cancellationToken)
        {
            _instance.SingleOrDefault(user => user.Id == id)?.Rename(name.Value);
            return Task.CompletedTask;
        }

        public Task ChangePasswordAsync(UserId id, PasswordHash passwordHash, RefreshTokenHash? keepTokenHash, CancellationToken cancellationToken)
        {
            _instance.SingleOrDefault(user => user.Id == id)?.ChangePassword(passwordHash);

            foreach (var token in RefreshTokenSingleton.Instance.Where(token =>
                token.UserId.Equals(id) && !token.IsRevoked && (keepTokenHash is null || !token.TokenHash.Equals(keepTokenHash))))
                token.RevokeToken();

            return Task.CompletedTask;
        }

        public int PasswordHashUpdateCount { get; private set; }

        public Task UpdatePasswordHashAsync(UserId id, PasswordHash passwordHash, CancellationToken cancellationToken)
        {
            PasswordHashUpdateCount++;
            _instance.SingleOrDefault(user => user.Id == id)?.ChangePassword(passwordHash);
            return Task.CompletedTask;
        }
    }
}
