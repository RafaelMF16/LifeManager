using LifeManager.Domain.Push;
using LifeManager.Domain.Push.Interfaces;
using LifeManager.Domain.Push.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Push
{
    public class PushSubscriptionRepository(LifeManagerDbContext dbContext) : IPushSubscriptionRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task SaveAsync(PushSubscription subscription, CancellationToken cancellationToken)
        {
            var userId = subscription.UserId.Value;

            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // Upsert on the unique endpoint. Another account on the same device takes it over (a new owner starts with a
            // fresh CreatedAt and no LastSuccessAt); the same owner only refreshes the keys and the label. The SET
            // expressions read the old row, so the CASEs compare with the previous owner.
            await _dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO "PushSubscriptions" ("UserId", "Endpoint", "P256dh", "Auth", "Label", "CreatedAt", "LastSuccessAt")
                VALUES ({userId}, {subscription.Endpoint}, {subscription.P256dh}, {subscription.Auth}, {subscription.Label}, {subscription.CreatedAt}, NULL)
                ON CONFLICT ("Endpoint") DO UPDATE SET
                    "P256dh" = EXCLUDED."P256dh",
                    "Auth" = EXCLUDED."Auth",
                    "Label" = EXCLUDED."Label",
                    "CreatedAt" = CASE WHEN "PushSubscriptions"."UserId" = EXCLUDED."UserId" THEN "PushSubscriptions"."CreatedAt" ELSE EXCLUDED."CreatedAt" END,
                    "LastSuccessAt" = CASE WHEN "PushSubscriptions"."UserId" = EXCLUDED."UserId" THEN "PushSubscriptions"."LastSuccessAt" END,
                    "UserId" = EXCLUDED."UserId"
                """, cancellationToken);

            // Keeps the user's most recently used devices, this one always among them.
            await _dbContext.Database.ExecuteSqlAsync($"""
                DELETE FROM "PushSubscriptions"
                WHERE "Id" IN (
                    SELECT "Id" FROM "PushSubscriptions"
                    WHERE "UserId" = {userId}
                    ORDER BY ("Endpoint" = {subscription.Endpoint}) DESC, COALESCE("LastSuccessAt", "CreatedAt") DESC, "Id" DESC
                    OFFSET {PushSubscription.MaxPerUser})
                """, cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);
        }

        public async Task DeleteAsync(UserId userId, string endpoint, CancellationToken cancellationToken)
        {
            await _dbContext.PushSubscriptions
                .Where(subscription => subscription.UserId == userId && subscription.Endpoint == endpoint)
                .ExecuteDeleteAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PushSubscription>> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.PushSubscriptions
                .AsNoTracking()
                .Where(subscription => subscription.UserId == userId)
                .OrderBy(subscription => subscription.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task DeleteByIdAsync(PushSubscriptionId id, UserId userId, CancellationToken cancellationToken)
        {
            await _dbContext.PushSubscriptions
                .Where(subscription => subscription.Id == id && subscription.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        public async Task MarkSuccessAsync(PushSubscriptionId id, UserId userId, DateTimeOffset at, CancellationToken cancellationToken)
        {
            await _dbContext.PushSubscriptions
                .Where(subscription => subscription.Id == id && subscription.UserId == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(subscription => subscription.LastSuccessAt, at), cancellationToken);
        }
    }
}
