using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences;
using LifeManager.Domain.UsersPreferences.Interfaces;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.UsersPreferences
{
    public class UserPreferencesRepository(LifeManagerDbContext dbContext) : IUserPreferencesRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<UserPreferences> AddAsync(UserPreferences userPreferences, CancellationToken cancellationToken)
        {
            _dbContext.Add(userPreferences);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return userPreferences;
        }

        public async Task<UserPreferences?> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.UserPreferences
                .AsNoTracking()
                .SingleOrDefaultAsync(userPreferences => userPreferences.UserId == userId, cancellationToken);
        }

        public async Task UpdateAsync(UserPreferences userPreferences, CancellationToken cancellationToken)
        {
            await _dbContext.UserPreferences
                .Where(storedPreferences => storedPreferences.Id == userPreferences.Id && storedPreferences.UserId == userPreferences.UserId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(storedPreferences => storedPreferences.Theme, userPreferences.Theme)
                    .SetProperty(storedPreferences => storedPreferences.Language, userPreferences.Language), cancellationToken);
        }
    }
}
