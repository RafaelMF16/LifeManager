using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Habits
{
    public class PlayerProfileRepository(LifeManagerDbContext dbContext) : IPlayerProfileRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<PlayerProfile?> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.PlayerProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
        }

        public async Task<PlayerProfile> ApplyAsync(UserId userId, Func<PlayerProfile, IReadOnlyList<GameLedgerEntry>> apply, CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var profile = await PlayerWallet.LockAsync(_dbContext, userId, cancellationToken);
            var entries = apply(profile);
            await PlayerWallet.SaveAsync(_dbContext, profile, entries, cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return profile;
        }
    }
}
