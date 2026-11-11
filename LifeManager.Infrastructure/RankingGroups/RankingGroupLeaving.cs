using LifeManager.Domain.RankingGroups;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.RankingGroups
{
    /// <summary>
    /// Leaving a group, inside the caller's database transaction: shared by a member who leaves
    /// (<see cref="RankingGroupRepository.LeaveAsync"/>) and by the account deletion, which must leave every group before
    /// deleting the user (the owner's foreign key cascades, so deleting an owner outright would delete the group of everyone).
    /// </summary>
    internal static class RankingGroupLeaving
    {
        /// <summary>The group's row, locked (<c>FOR UPDATE</c>) until the caller's transaction ends; null when it's gone.</summary>
        public static async Task<RankingGroup?> LockGroupAsync(LifeManagerDbContext dbContext, RankingGroupId groupId, CancellationToken cancellationToken)
        {
            var id = groupId.Value;
            var lockedGroups = await dbContext.RankingGroups
                .FromSql($"""SELECT * FROM "RankingGroups" WHERE "Id" = {id} FOR UPDATE""")
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return lockedGroups.SingleOrDefault();
        }

        /// <summary>
        /// Takes the user out of the group under its lock: the oldest member becomes the owner when the owner leaves, and
        /// the last one out deletes the group (<see cref="RankingGroupMembership.OwnerAfterLeaving"/>). False when the
        /// group is gone or the user wasn't in it (nothing written).
        /// </summary>
        public static async Task<bool> LeaveAsync(LifeManagerDbContext dbContext, RankingGroupId groupId, UserId userId, CancellationToken cancellationToken)
        {
            var group = await LockGroupAsync(dbContext, groupId, cancellationToken);
            if (group is null)
                return false;

            var members = await dbContext.RankingGroupMembers
                .AsNoTracking()
                .Where(member => member.GroupId == groupId)
                .ToListAsync(cancellationToken);
            if (members.All(member => member.UserId != userId))
                return false;

            var newOwner = RankingGroupMembership.OwnerAfterLeaving(group, members, userId);
            if (newOwner is null)
            {
                // The last one out: the members and everything else of the group go with it (cascade).
                await dbContext.RankingGroups.Where(storedGroup => storedGroup.Id == groupId).ExecuteDeleteAsync(cancellationToken);
            }
            else
            {
                await dbContext.RankingGroupMembers
                    .Where(member => member.GroupId == groupId && member.UserId == userId)
                    .ExecuteDeleteAsync(cancellationToken);
                await dbContext.RankingGroups
                    .Where(storedGroup => storedGroup.Id == groupId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(storedGroup => storedGroup.OwnerUserId, newOwner), cancellationToken);
            }

            return true;
        }
    }
}
