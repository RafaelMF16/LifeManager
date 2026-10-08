using LifeManager.Domain.Habits;
using LifeManager.Domain.Rewards;
using LifeManager.Domain.Rewards.Interfaces;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Habits;
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Rewards
{
    public class RewardRedemptionRepository(LifeManagerDbContext dbContext) : IRewardRedemptionRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<RewardRedemption?> GetByIdAsync(RewardRedemptionId redemptionId, UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.RewardRedemptions
                .AsNoTracking()
                .SingleOrDefaultAsync(redemption => redemption.Id == redemptionId && redemption.UserId == userId, cancellationToken);
        }

        public async Task<PagedList<RewardRedemption>> GetPagedByUserIdAsync(UserId userId, PageRequest pageRequest, CancellationToken cancellationToken)
        {
            return await _dbContext.RewardRedemptions
                .AsNoTracking()
                .Where(redemption => redemption.UserId == userId)
                .OrderByDescending(redemption => redemption.RedeemedAt)
                .ThenByDescending(redemption => redemption.Id)
                .ToPagedListAsync(pageRequest, cancellationToken);
        }

        public async Task<PlayerProfile?> RedeemAsync(
            RewardRedemption redemption,
            Func<PlayerProfile, IReadOnlyList<GameLedgerEntry>?> decide,
            CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var profile = await PlayerWallet.LockAsync(_dbContext, redemption.UserId, cancellationToken);

            var entries = decide(profile);
            if (entries is null)
                return null;

            _dbContext.RewardRedemptions.Add(redemption);
            await PlayerWallet.SaveAsync(_dbContext, profile, entries, cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return profile;
        }

        public async Task<PlayerProfile?> UndoAsync(
            RewardRedemption redemption,
            Func<PlayerProfile, IReadOnlyList<GameLedgerEntry>> apply,
            CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var profile = await PlayerWallet.LockAsync(_dbContext, redemption.UserId, cancellationToken);

            // Under the lock, so an undo racing another one for the same redemption finds it already undone.
            var updatedRows = await _dbContext.RewardRedemptions
                .Where(storedRedemption => storedRedemption.Id == redemption.Id
                    && storedRedemption.UserId == redemption.UserId
                    && storedRedemption.UndoneAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(storedRedemption => storedRedemption.UndoneAt, redemption.UndoneAt), cancellationToken);
            if (updatedRows == 0)
                return null;

            await PlayerWallet.SaveAsync(_dbContext, profile, apply(profile), cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return profile;
        }
    }
}
