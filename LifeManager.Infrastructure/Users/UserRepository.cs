using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Users
{
    public class UserRepository(LifeManagerDbContext dbContext) : IUserRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

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

        public async Task UpdateNameAsync(UserId id, UserName name, CancellationToken cancellationToken)
        {
            await _dbContext.Users
                .Where(user => user.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.Name, name), cancellationToken);
        }

        public async Task ChangePasswordAsync(UserId id, PasswordHash passwordHash, RefreshTokenHash? keepTokenHash, CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            await _dbContext.Users
                .Where(user => user.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.PasswordHash, passwordHash), cancellationToken);

            await _dbContext.RefreshTokens
                .Where(refreshToken => refreshToken.UserId == id && !refreshToken.IsRevoked
                    && (keepTokenHash == null || refreshToken.TokenHash != keepTokenHash))
                .ExecuteUpdateAsync(setters => setters.SetProperty(refreshToken => refreshToken.IsRevoked, true), cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);
        }

        public async Task UpdatePasswordHashAsync(UserId id, PasswordHash passwordHash, CancellationToken cancellationToken)
        {
            await _dbContext.Users
                .Where(user => user.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.PasswordHash, passwordHash), cancellationToken);
        }
    }
}
