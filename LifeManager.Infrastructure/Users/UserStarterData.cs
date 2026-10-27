using LifeManager.Domain.Categories;
using LifeManager.Domain.Users;
using LifeManager.Domain.UsersPreferences;
using LifeManager.Domain.UsersPreferences.Enums;
using LifeManager.Infrastructure.Postgres;

namespace LifeManager.Infrastructure.Users
{
    internal static class UserStarterData
    {
        /// <summary>
        /// Adds the user, their preferences and the default categories in the preferences' language. Runs inside the
        /// caller's database transaction, so the account is stored whole or not at all.
        /// </summary>
        public static async Task AddAsync(LifeManagerDbContext dbContext, User user, Theme? theme, Language? language, CancellationToken cancellationToken)
        {
            // The user's id only exists after this save, and the starting data needs it.
            dbContext.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);

            var preferences = UserPreferences.CreateDefault(user.Id!, theme, language);
            dbContext.Add(preferences);
            dbContext.AddRange(DefaultCategories.CreateFor(user.Id!, preferences.Language));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
