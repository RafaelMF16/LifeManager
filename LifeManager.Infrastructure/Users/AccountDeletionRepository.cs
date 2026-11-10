using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.RankingGroups;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Users
{
    public class AccountDeletionRepository(LifeManagerDbContext dbContext) : IAccountDeletionRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<IReadOnlyList<UserId>> GetDueUserIdsAsync(DateTimeOffset now, int maxCount, CancellationToken cancellationToken)
            => await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.DeletionScheduledFor != null && user.DeletionScheduledFor <= now)
                .OrderBy(user => user.DeletionScheduledFor)
                .ThenBy(user => user.Id)
                .Select(user => user.Id!)
                .Take(maxCount)
                .ToListAsync(cancellationToken);

        public async Task<bool> DeleteIfDueAsync(UserId userId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // Lock order: the user's groups (by id) first, then the user's row. A member leaving one of those groups locks
            // the group and may then make this user its owner, which waits on the user's row: taking the groups first
            // means that member waits here instead of deadlocking. The user's groups can only shrink meanwhile (nobody
            // else adds them), and leaving a group they're no longer in writes nothing.
            var groupIds = await _dbContext.RankingGroupMembers
                .AsNoTracking()
                .Where(member => member.UserId == userId)
                .Select(member => member.GroupId)
                .OrderBy(groupId => groupId)
                .ToListAsync(cancellationToken);

            foreach (var groupId in groupIds)
                await RankingGroupLeaving.LockGroupAsync(_dbContext, groupId, cancellationToken);

            // Re-checked under the lock: a sign-in that cancelled the deletion since the lookup keeps the account.
            var id = userId.Value;
            var due = await _dbContext.Database
                .SqlQuery<int>($"""SELECT "Id" AS "Value" FROM "Users" WHERE "Id" = {id} AND "DeletionScheduledFor" <= {now} FOR UPDATE""")
                .ToListAsync(cancellationToken);
            if (due.Count == 0)
                return false;

            foreach (var groupId in groupIds)
                await RankingGroupLeaving.LeaveAsync(_dbContext, groupId, userId, cancellationToken);

            await _dbContext.Users.Where(user => user.Id == userId).ExecuteDeleteAsync(cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return true;
        }
    }
}
