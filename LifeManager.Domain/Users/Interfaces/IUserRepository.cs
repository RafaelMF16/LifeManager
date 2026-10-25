using LifeManager.Domain.UsersPreferences.Enums;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Users.Interfaces
{
    public interface IUserRepository
    {
        /// <summary>
        /// Adds the user together with their starting data, in one database transaction: the preferences
        /// (<see cref="UsersPreferences.UserPreferences.CreateDefault"/>) and the default categories in the preferences'
        /// language (<see cref="Categories.DefaultCategories.CreateFor"/>).
        /// </summary>
        Task<User> AddWithStarterDataAsync(User user, Theme? theme, Language? language, CancellationToken cancellationToken);
        Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken);
        Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken);
        Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken);
    }
}
