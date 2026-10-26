using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences.Enums;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Users
{
    public class UserRepository(LifeManagerDbContext dbContext) : IUserRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<User> AddWithStarterDataAsync(User user, Theme? theme, Language? language, CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            await UserStarterData.AddAsync(_dbContext, user, theme, language, cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return user;
        }

        public async Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken)
        {
            return await _dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
        }

        public async Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken)
        {
            return await _dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Email == email, cancellationToken);
        }

        public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken)
        {
            return await _dbContext.Users
                .AnyAsync(user => user.Email == email, cancellationToken);
        }
    }
}
