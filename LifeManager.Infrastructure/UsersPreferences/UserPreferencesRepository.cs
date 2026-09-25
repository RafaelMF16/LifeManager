using LifeManager.Domain.Shared.Results;
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

        public UserPreferences Add(UserPreferences userPreferences)
        {
            _dbContext.Add(userPreferences);
            _dbContext.SaveChanges();

            return userPreferences;
        }

        public UserPreferences Update(UserPreferences userPreferences)
        {
            _dbContext.Update(userPreferences);
            _dbContext.SaveChanges();

            return userPreferences;
        }

        public UserPreferences? GetUserPreferencesByUserId(UserId userId)
        {
            return _dbContext.UserPreferences
                .AsNoTracking()
                .SingleOrDefault(userPreferences => userPreferences.UserId == userId);
        }
    }
}