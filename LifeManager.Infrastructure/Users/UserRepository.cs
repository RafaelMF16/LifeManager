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
    }
}
