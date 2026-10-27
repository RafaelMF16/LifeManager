using LifeManager.Domain.AccountHolders;
using LifeManager.Domain.AccountHolders.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.AccountHolders
{
    public class AccountHolderRepository(LifeManagerDbContext dbContext) : IAccountHolderRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<IReadOnlyList<AccountHolder>> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.AccountHolders
                .AsNoTracking()
                .Where(accountHolder => accountHolder.UserId == userId)
                .OrderBy(accountHolder => accountHolder.Position)
                .ToListAsync(cancellationToken);
        }

        public async Task ReplaceAsync(AccountHolderList accountHolderList, CancellationToken cancellationToken)
        {
            var userId = accountHolderList.UserId.Value;

            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // Locks the user's row so two saves of the same list run one after the other: the last one wins whole,
            // instead of both deleting first and then clashing on (UserId, Position). NO KEY UPDATE, so inserts that
            // only reference the user (their FK check takes KEY SHARE) don't wait for it.
            await _dbContext.Database.ExecuteSqlAsync($"""SELECT 1 FROM "Users" WHERE "Id" = {userId} FOR NO KEY UPDATE""", cancellationToken);

            await _dbContext.AccountHolders
                .Where(accountHolder => accountHolder.UserId == accountHolderList.UserId)
                .ExecuteDeleteAsync(cancellationToken);

            _dbContext.AccountHolders.AddRange(accountHolderList.Holders);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);
        }
    }
}
