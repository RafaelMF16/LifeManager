using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.RankingGroups;
using LifeManager.Domain.RankingGroups.Errors;
using LifeManager.Domain.RankingGroups.Interfaces;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Habits;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.RankingGroups
{
    /// <summary>
    /// Lock order, the same in every write that takes both: the user's player profile (<see cref="PlayerWallet.LockAsync"/>)
    /// first, then the group's row. Creating and joining take the user's lock so their group count can't go stale;
    /// joining, leaving and removing take the group's so its member count and owner can't.
    /// </summary>
    public class RankingGroupRepository(LifeManagerDbContext dbContext) : IRankingGroupRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<IReadOnlyList<RankingGroupSummary>> GetSummariesAsync(UserId userId, CancellationToken cancellationToken)
        {
            var rows = await _dbContext.RankingGroups
                .AsNoTracking()
                .Where(group => _dbContext.RankingGroupMembers.Any(member => member.GroupId == group.Id && member.UserId == userId))
                .Select(group => new
                {
                    group.Id,
                    group.Name,
                    group.OwnerUserId,
                    group.CreatedOn,
                    MemberCount = _dbContext.RankingGroupMembers.Count(member => member.GroupId == group.Id)
                })
                .ToListAsync(cancellationToken);

            return [.. rows
                .Select(row => new RankingGroupSummary(row.Id!, row.Name.Value, row.MemberCount, row.OwnerUserId == userId, row.CreatedOn))
                .OrderBy(summary => summary.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(summary => summary.Id.Value)];
        }

        public async Task<RankingGroupView?> GetForMemberAsync(RankingGroupId groupId, UserId userId, CancellationToken cancellationToken)
        {
            var isMember = await _dbContext.RankingGroupMembers.AnyAsync(member => member.GroupId == groupId && member.UserId == userId, cancellationToken);

            return isMember ? await GetViewAsync(groupId, cancellationToken) : null;
        }

        public async Task<RankingGroupView?> GetViewAsync(RankingGroupId groupId, CancellationToken cancellationToken)
        {
            var group = await _dbContext.RankingGroups
                .AsNoTracking()
                .SingleOrDefaultAsync(storedGroup => storedGroup.Id == groupId, cancellationToken);
            if (group is null)
                return null;

            var members = await _dbContext.RankingGroupMembers
                .AsNoTracking()
                .Where(member => member.GroupId == groupId)
                .OrderBy(member => member.JoinedAt)
                .ThenBy(member => member.Id)
                .Select(member => new
                {
                    member.Id,
                    member.UserId,
                    member.JoinedAt,
                    member.JoinedOn,
                    Name = _dbContext.Users.Where(user => user.Id == member.UserId).Select(user => user.Name).First(),
                    Profile = _dbContext.PlayerProfiles
                        .Where(profile => profile.UserId == member.UserId)
                        .Select(profile => new { profile.Avatar, profile.Title })
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            return new RankingGroupView(group, [.. members.Select(member => new RankingMemberView(
                member.Id!,
                member.UserId,
                member.Name.Value,
                member.Profile?.Avatar ?? PlayerAvatar.Cat,
                member.Profile?.Title,
                member.JoinedAt,
                member.JoinedOn))]);
        }

        public async Task<IReadOnlyList<RankingGroupMember>> GetMembersOfUserGroupsAsync(UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.RankingGroupMembers
                .AsNoTracking()
                .Where(member => _dbContext.RankingGroupMembers.Any(mine => mine.GroupId == member.GroupId && mine.UserId == userId))
                .ToListAsync(cancellationToken);
        }

        public async Task<RankingGroupPreview?> GetPreviewAsync(InviteCode inviteCode, UserId userId, CancellationToken cancellationToken)
        {
            var row = await _dbContext.RankingGroups
                .AsNoTracking()
                .Where(group => group.InviteCode == inviteCode)
                .Select(group => new
                {
                    group.Id,
                    group.Name,
                    OwnerName = _dbContext.Users.Where(user => user.Id == group.OwnerUserId).Select(user => user.Name).First(),
                    MemberCount = _dbContext.RankingGroupMembers.Count(member => member.GroupId == group.Id),
                    AlreadyMember = _dbContext.RankingGroupMembers.Any(member => member.GroupId == group.Id && member.UserId == userId)
                })
                .SingleOrDefaultAsync(cancellationToken);

            return row is null ? null : new RankingGroupPreview(row.Id!, row.Name.Value, row.OwnerName.Value, row.MemberCount, row.AlreadyMember);
        }

        public Task<bool> IsInviteCodeTakenAsync(InviteCode inviteCode, CancellationToken cancellationToken)
            => _dbContext.RankingGroups.AnyAsync(group => group.InviteCode == inviteCode, cancellationToken);

        public async Task<Result<RankingGroupId>> CreateAsync(RankingGroup group, DateOnly joinedOn, CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            await PlayerWallet.LockAsync(_dbContext, group.OwnerUserId, cancellationToken);

            var allowed = RankingGroupMembership.CanCreate(await CountGroupsAsync(group.OwnerUserId, cancellationToken));
            if (!allowed.IsSuccess)
                return allowed.Error!;

            _dbContext.RankingGroups.Add(group);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _dbContext.RankingGroupMembers.Add(RankingGroupMember.Create(group.Id!, group.OwnerUserId, group.CreatedAt, joinedOn));
            await _dbContext.SaveChangesAsync(cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return group.Id!;
        }

        public async Task<Result<RankingGroupId>> JoinAsync(InviteCode inviteCode, UserId userId, DateTimeOffset joinedAt, DateOnly joinedOn, CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            await PlayerWallet.LockAsync(_dbContext, userId, cancellationToken);

            var code = inviteCode.Value;
            var lockedGroups = await _dbContext.RankingGroups
                .FromSql($"""SELECT * FROM "RankingGroups" WHERE "InviteCode" = {code} FOR UPDATE""")
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            if (lockedGroups.Count == 0)
                return RankingGroupErrors.InviteNotFound;

            var groupId = lockedGroups[0].Id!;
            var allowed = RankingGroupMembership.CanJoin(
                await CountGroupsAsync(userId, cancellationToken),
                await _dbContext.RankingGroupMembers.CountAsync(member => member.GroupId == groupId, cancellationToken),
                await _dbContext.RankingGroupMembers.AnyAsync(member => member.GroupId == groupId && member.UserId == userId, cancellationToken));
            if (!allowed.IsSuccess)
                return allowed.Error!;

            _dbContext.RankingGroupMembers.Add(RankingGroupMember.Create(groupId, userId, joinedAt, joinedOn));
            await _dbContext.SaveChangesAsync(cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return groupId;
        }

        public async Task<bool> SaveNameAsync(RankingGroup group, UserId ownerId, CancellationToken cancellationToken)
            => await _dbContext.RankingGroups
                .Where(storedGroup => storedGroup.Id == group.Id && storedGroup.OwnerUserId == ownerId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(storedGroup => storedGroup.Name, group.Name), cancellationToken) > 0;

        public async Task<bool> SaveInviteCodeAsync(RankingGroup group, UserId ownerId, CancellationToken cancellationToken)
            => await _dbContext.RankingGroups
                .Where(storedGroup => storedGroup.Id == group.Id && storedGroup.OwnerUserId == ownerId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(storedGroup => storedGroup.InviteCode, group.InviteCode), cancellationToken) > 0;

        public async Task<bool> LeaveAsync(RankingGroupId groupId, UserId userId, CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var group = await LockGroupAsync(groupId, cancellationToken);
            if (group is null)
                return false;

            var members = await _dbContext.RankingGroupMembers
                .AsNoTracking()
                .Where(member => member.GroupId == groupId)
                .ToListAsync(cancellationToken);
            if (members.All(member => member.UserId != userId))
                return false;

            var newOwner = RankingGroupMembership.OwnerAfterLeaving(group, members, userId);
            if (newOwner is null)
            {
                // The last one out: the members and everything else of the group go with it (cascade).
                await _dbContext.RankingGroups.Where(storedGroup => storedGroup.Id == groupId).ExecuteDeleteAsync(cancellationToken);
            }
            else
            {
                await _dbContext.RankingGroupMembers
                    .Where(member => member.GroupId == groupId && member.UserId == userId)
                    .ExecuteDeleteAsync(cancellationToken);
                await _dbContext.RankingGroups
                    .Where(storedGroup => storedGroup.Id == groupId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(storedGroup => storedGroup.OwnerUserId, newOwner), cancellationToken);
            }

            await databaseTransaction.CommitAsync(cancellationToken);

            return true;
        }

        public async Task<Result> RemoveMemberAsync(
            RankingGroupId groupId, RankingGroupMemberId memberId, UserId ownerId, CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var group = await LockGroupAsync(groupId, cancellationToken);
            if (group is null)
                return RankingGroupErrors.NotFound;

            // The owner may have left (passing the group on) since the service read it.
            if (!group.IsOwnedBy(ownerId))
                return RankingGroupErrors.NotOwner;

            // Never the owner: they leave instead, which passes the group on.
            var removed = await _dbContext.RankingGroupMembers
                .Where(member => member.GroupId == groupId && member.Id == memberId && member.UserId != group.OwnerUserId)
                .ExecuteDeleteAsync(cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return removed > 0 ? Result.Success() : RankingGroupErrors.MemberNotFound;
        }

        private Task<int> CountGroupsAsync(UserId userId, CancellationToken cancellationToken)
            => _dbContext.RankingGroupMembers.CountAsync(member => member.UserId == userId, cancellationToken);

        private async Task<RankingGroup?> LockGroupAsync(RankingGroupId groupId, CancellationToken cancellationToken)
        {
            var id = groupId.Value;
            var lockedGroups = await _dbContext.RankingGroups
                .FromSql($"""SELECT * FROM "RankingGroups" WHERE "Id" = {id} FOR UPDATE""")
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return lockedGroups.SingleOrDefault();
        }
    }
}
