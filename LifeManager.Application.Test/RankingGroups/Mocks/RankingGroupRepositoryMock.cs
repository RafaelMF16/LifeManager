using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.RankingGroups;
using LifeManager.Domain.RankingGroups.Errors;
using LifeManager.Domain.RankingGroups.Interfaces;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.RankingGroups.Mocks
{
    /// <summary>
    /// Mirrors RankingGroupRepository over the singletons: the same membership scoping, limits and ownership rules.
    /// <see cref="BeforeNextWrite"/> plays a request that changed the group between the service's read and its write.
    /// </summary>
    public class RankingGroupRepositoryMock : IRankingGroupRepository
    {
        private static readonly object Gate = new();

        public Task<IReadOnlyList<RankingGroupSummary>> GetSummariesAsync(UserId userId, CancellationToken cancellationToken)
        {
            IReadOnlyList<RankingGroupSummary> summaries = [.. RankingGroupSingleton.Instance
                .Where(group => IsMember(group.Id!, userId))
                .Select(group => new RankingGroupSummary(group.Id!, group.Name.Value, MembersOf(group.Id!).Count, group.IsOwnedBy(userId), group.CreatedOn))
                .OrderBy(summary => summary.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(summary => summary.Id.Value)];

            return Task.FromResult(summaries);
        }

        public Task<RankingGroupView?> GetForMemberAsync(RankingGroupId groupId, UserId userId, CancellationToken cancellationToken)
            => IsMember(groupId, userId) ? GetViewAsync(groupId, cancellationToken) : Task.FromResult<RankingGroupView?>(null);

        public Task<RankingGroupView?> GetViewAsync(RankingGroupId groupId, CancellationToken cancellationToken)
        {
            var group = RankingGroupSingleton.Instance.SingleOrDefault(candidate => candidate.Id == groupId);
            if (group is null)
                return Task.FromResult<RankingGroupView?>(null);

            // A detached copy, like an AsNoTracking read: changing it doesn't change the stored group.
            var copy = RankingGroup.FromPersistence(group.Id!.Value, group.Name.Value, group.OwnerUserId.Value, group.InviteCode.Value, group.CreatedAt, group.CreatedOn);

            return Task.FromResult<RankingGroupView?>(new RankingGroupView(copy, [.. MembersOf(groupId)
                .OrderBy(member => member.JoinedAt)
                .ThenBy(member => member.Id!.Value)
                .Select(ToView)]));
        }

        public Task<IReadOnlyList<RankingGroupMember>> GetMembersOfUserGroupsAsync(UserId userId, CancellationToken cancellationToken)
        {
            IReadOnlyList<RankingGroupMember> members = [.. RankingGroupMemberSingleton.Instance.Where(member => IsMember(member.GroupId, userId))];

            return Task.FromResult(members);
        }

        public Task<RankingGroupPreview?> GetPreviewAsync(InviteCode inviteCode, UserId userId, CancellationToken cancellationToken)
        {
            var group = RankingGroupSingleton.Instance.SingleOrDefault(candidate => candidate.InviteCode.Equals(inviteCode));

            return Task.FromResult(group is null
                ? null
                : new RankingGroupPreview(group.Id!, group.Name.Value, NameOf(group.OwnerUserId), MembersOf(group.Id!).Count, IsMember(group.Id!, userId)));
        }

        public Task<bool> IsInviteCodeTakenAsync(InviteCode inviteCode, CancellationToken cancellationToken)
            => Task.FromResult(RankingGroupSingleton.Instance.Any(group => group.InviteCode.Equals(inviteCode)));

        public Task<Result<RankingGroupId>> CreateAsync(RankingGroup group, DateOnly joinedOn, CancellationToken cancellationToken)
        {
            lock (Gate)
            {
                var allowed = RankingGroupMembership.CanCreate(GroupCount(group.OwnerUserId));
                if (!allowed.IsSuccess)
                    return Task.FromResult<Result<RankingGroupId>>(allowed.Error!);

                group.AssignId(NextId(RankingGroupSingleton.Instance.Select(stored => stored.Id!.Value)));
                RankingGroupSingleton.Instance.Add(group);
                AddMember(group.Id!, group.OwnerUserId, group.CreatedAt, joinedOn);

                return Task.FromResult<Result<RankingGroupId>>(group.Id!);
            }
        }

        public Task<Result<RankingGroupId>> JoinAsync(InviteCode inviteCode, UserId userId, DateTimeOffset joinedAt, DateOnly joinedOn, CancellationToken cancellationToken)
        {
            lock (Gate)
            {
                var group = RankingGroupSingleton.Instance.SingleOrDefault(candidate => candidate.InviteCode.Equals(inviteCode));
                if (group is null)
                    return Task.FromResult<Result<RankingGroupId>>(RankingGroupErrors.InviteNotFound);

                var allowed = RankingGroupMembership.CanJoin(GroupCount(userId), MembersOf(group.Id!).Count, IsMember(group.Id!, userId));
                if (!allowed.IsSuccess)
                    return Task.FromResult<Result<RankingGroupId>>(allowed.Error!);

                AddMember(group.Id!, userId, joinedAt, joinedOn);

                return Task.FromResult<Result<RankingGroupId>>(group.Id!);
            }
        }

        public Task<bool> SaveNameAsync(RankingGroup group, UserId ownerId, CancellationToken cancellationToken)
            => Task.FromResult(SaveOwned(group.Id!, ownerId, stored => RankingGroup.FromPersistence(
                stored.Id!.Value, group.Name.Value, stored.OwnerUserId.Value, stored.InviteCode.Value, stored.CreatedAt, stored.CreatedOn)));

        public Task<bool> SaveInviteCodeAsync(RankingGroup group, UserId ownerId, CancellationToken cancellationToken)
            => Task.FromResult(SaveOwned(group.Id!, ownerId, stored => RankingGroup.FromPersistence(
                stored.Id!.Value, stored.Name.Value, stored.OwnerUserId.Value, group.InviteCode.Value, stored.CreatedAt, stored.CreatedOn)));

        /// <summary>Like the conditional update: one column over what is stored now, only while the owner still owns it.</summary>
        private static bool SaveOwned(RankingGroupId groupId, UserId ownerId, Func<RankingGroup, RankingGroup> change)
        {
            RunBeforeNextWrite();

            lock (Gate)
            {
                var index = RankingGroupSingleton.Instance.FindIndex(stored => stored.Id == groupId && stored.IsOwnedBy(ownerId));
                if (index < 0)
                    return false;

                RankingGroupSingleton.Instance[index] = change(RankingGroupSingleton.Instance[index]);
                return true;
            }
        }

        public Task<bool> LeaveAsync(RankingGroupId groupId, UserId userId, CancellationToken cancellationToken)
        {
            lock (Gate)
            {
                var group = RankingGroupSingleton.Instance.SingleOrDefault(candidate => candidate.Id == groupId);
                var members = MembersOf(groupId);
                if (group is null || members.All(member => member.UserId != userId))
                    return Task.FromResult(false);

                var newOwner = RankingGroupMembership.OwnerAfterLeaving(group, members, userId);
                if (newOwner is null)
                {
                    RankingGroupSingleton.Instance.Remove(group);
                    RankingGroupMemberSingleton.Instance.RemoveAll(member => member.GroupId == groupId);
                    GroupDeleted?.Invoke(groupId);
                }
                else
                {
                    RankingGroupMemberSingleton.Instance.RemoveAll(member => member.GroupId == groupId && member.UserId == userId);
                    RankingGroupSingleton.Instance[RankingGroupSingleton.Instance.IndexOf(group)] = RankingGroup.FromPersistence(
                        group.Id!.Value, group.Name.Value, newOwner.Value, group.InviteCode.Value, group.CreatedAt, group.CreatedOn);
                }

                MemberLeft?.Invoke(groupId, userId);
                return Task.FromResult(true);
            }
        }

        public Task<Result> RemoveMemberAsync(RankingGroupId groupId, RankingGroupMemberId memberId, UserId ownerId, CancellationToken cancellationToken)
        {
            RunBeforeNextWrite();

            lock (Gate)
            {
                var group = RankingGroupSingleton.Instance.SingleOrDefault(candidate => candidate.Id == groupId);
                if (group is null)
                    return Task.FromResult<Result>(RankingGroupErrors.NotFound);
                if (!group.IsOwnedBy(ownerId))
                    return Task.FromResult<Result>(RankingGroupErrors.NotOwner);

                var member = MembersOf(groupId).SingleOrDefault(candidate => candidate.Id == memberId && candidate.UserId != group.OwnerUserId);
                if (member is null)
                    return Task.FromResult<Result>(RankingGroupErrors.MemberNotFound);

                RankingGroupMemberSingleton.Instance.Remove(member);
                MemberLeft?.Invoke(groupId, member.UserId);
                return Task.FromResult(Result.Success());
            }
        }

        /// <summary>Runs once right before the next owner-only write: a concurrent request that got there first.</summary>
        public static Action? BeforeNextWrite { get; set; }

        private static void RunBeforeNextWrite()
        {
            // Cleared first: the concurrent request may write too.
            var beforeNextWrite = BeforeNextWrite;
            BeforeNextWrite = null;
            beforeNextWrite?.Invoke();
        }

        /// <summary>What the database's cascades do when a member goes (for the mocks of the tables that hang on them).</summary>
        public static event Action<RankingGroupId, UserId>? MemberLeft;

        public static event Action<RankingGroupId>? GroupDeleted;

        internal static RankingMemberView ToView(RankingGroupMember member)
        {
            var profile = PlayerProfileSingleton.Instance.SingleOrDefault(candidate => candidate.UserId == member.UserId);

            return new RankingMemberView(member.Id!, member.UserId, NameOf(member.UserId), profile?.Avatar ?? PlayerAvatar.Cat, profile?.Title, member.JoinedAt, member.JoinedOn);
        }

        private static string NameOf(UserId userId)
            => UserSingleton.Instance.Single(user => user.Id == userId).Name.Value;

        private static List<RankingGroupMember> MembersOf(RankingGroupId groupId)
            => [.. RankingGroupMemberSingleton.Instance.Where(member => member.GroupId == groupId)];

        private static bool IsMember(RankingGroupId groupId, UserId userId)
            => RankingGroupMemberSingleton.Instance.Any(member => member.GroupId == groupId && member.UserId == userId);

        private static int GroupCount(UserId userId)
            => RankingGroupMemberSingleton.Instance.Count(member => member.UserId == userId);

        private static void AddMember(RankingGroupId groupId, UserId userId, DateTimeOffset joinedAt, DateOnly joinedOn)
        {
            var member = RankingGroupMember.Create(groupId, userId, joinedAt, joinedOn);
            member.AssignId(NextId(RankingGroupMemberSingleton.Instance.Select(stored => stored.Id!.Value)));
            RankingGroupMemberSingleton.Instance.Add(member);
        }

        private static int NextId(IEnumerable<int> ids)
            => ids.DefaultIfEmpty(0).Max() + 1;
    }
}
