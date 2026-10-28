using LifeManager.Domain.GameItems;
using LifeManager.Domain.GameItems.Enums;
using LifeManager.Domain.GameItems.Interfaces;
using LifeManager.Domain.GameItems.ValueObjects;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Habits;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.GameItems
{
    public class GameItemPurchaseRepository(LifeManagerDbContext dbContext) : IGameItemPurchaseRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<GameItemPurchase?> GetByIdAsync(GameItemPurchaseId purchaseId, UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.GameItemPurchases
                .AsNoTracking()
                .SingleOrDefaultAsync(purchase => purchase.Id == purchaseId && purchase.UserId == userId, cancellationToken);
        }

        public async Task<IReadOnlyList<GameItemPurchase>> GetByDayAsync(UserId userId, DateOnly day, CancellationToken cancellationToken)
        {
            return await _dbContext.GameItemPurchases
                .AsNoTracking()
                .Where(purchase => purchase.UserId == userId && purchase.PurchasedOn == day)
                .OrderBy(purchase => purchase.PurchasedAt)
                .ThenBy(purchase => purchase.Id)
                .ToListAsync(cancellationToken);
        }

        public Task<int> CountActiveSinceAsync(UserId userId, GameItemKind kind, DateOnly from, CancellationToken cancellationToken)
            => ActiveSince(userId, kind, from).CountAsync(cancellationToken);

        public async Task<PlayerProfile?> PurchaseAsync(
            UserId userId,
            GameItemKind kind,
            DateOnly periodStart,
            Func<PlayerProfile, int, GameItemPurchaseDecision?> decide,
            CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var profile = await PlayerWallet.LockAsync(_dbContext, userId, cancellationToken);

            // Under the lock: every purchase of the user waits for it, so this count can't go stale before the insert.
            var purchasedInPeriod = await ActiveSince(userId, kind, periodStart).CountAsync(cancellationToken);

            var decision = decide(profile, purchasedInPeriod);
            if (decision is null)
                return null;

            _dbContext.GameItemPurchases.Add(decision.Purchase);
            await PlayerWallet.SaveAsync(_dbContext, profile, decision.Entries, cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return profile;
        }

        public async Task<PlayerProfile?> UndoAsync(
            GameItemPurchase purchase,
            Func<PlayerProfile, IReadOnlyList<GameLedgerEntry>?> decide,
            CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var profile = await PlayerWallet.LockAsync(_dbContext, purchase.UserId, cancellationToken);

            // Under the lock, so an undo racing another one for the same purchase finds it already undone.
            var updatedRows = await _dbContext.GameItemPurchases
                .Where(storedPurchase => storedPurchase.Id == purchase.Id
                    && storedPurchase.UserId == purchase.UserId
                    && storedPurchase.UndoneAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(storedPurchase => storedPurchase.UndoneAt, purchase.UndoneAt), cancellationToken);
            if (updatedRows == 0)
                return null;

            // Disposing the transaction without committing rolls the UndoneAt back.
            var entries = decide(profile);
            if (entries is null)
                return null;

            await PlayerWallet.SaveAsync(_dbContext, profile, entries, cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return profile;
        }

        private IQueryable<GameItemPurchase> ActiveSince(UserId userId, GameItemKind kind, DateOnly from)
            => _dbContext.GameItemPurchases
                .Where(purchase => purchase.UserId == userId
                    && purchase.Kind == kind
                    && purchase.PurchasedOn >= from
                    && purchase.UndoneAt == null);
    }
}
