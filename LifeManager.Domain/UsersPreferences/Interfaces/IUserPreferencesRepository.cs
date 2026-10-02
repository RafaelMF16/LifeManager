using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.UsersPreferences.Interfaces
{
    public interface IUserPreferencesRepository
    {
        Task<UserPreferences> AddAsync(UserPreferences userPreferences, CancellationToken cancellationToken);
        Task<UserPreferences?> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken);
        Task UpdateAsync(UserPreferences userPreferences, CancellationToken cancellationToken);
    }
}
