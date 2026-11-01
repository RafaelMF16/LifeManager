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
    /// <summary>Mirrors RankingGroupRepository over the singletons: the same membership scoping, limits and ownership rules.</summary>
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

        public Task UpdateAsync(RankingGroup group, CancellationToken cancellationToken)
        {
            var index = RankingGroupSingleton.Instance.FindIndex(stored => stored.Id == group.Id);
            var stored = RankingGroupSingleton.Instance[index];
            RankingGroupSingleton.Instance[index] = RankingGroup.FromPersistence(
                stored.Id!.Value, group.Name.Value, stored.OwnerUserId.Value, group.InviteCode.Value, stored.CreatedAt, stored.CreatedOn);

            return Task.CompletedTask;
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

        public Task<bool> RemoveMemberAsync(RankingGroupId groupId, RankingGroupMemberId memberId, CancellationToken cancellationToken)
        {
            lock (Gate)
            {
                var group = RankingGroupSingleton.Instance.SingleOrDefault(candidate => candidate.Id == groupId);
                var member = MembersOf(groupId).SingleOrDefault(candidate => candidate.Id == memberId && group is not null && candidate.UserId != group.OwnerUserId);
                if (member is null)
                    return Task.FromResult(false);

                RankingGroupMemberSingleton.Instance.Remove(member);
                MemberLeft?.Invoke(groupId, member.UserId);
                return Task.FromResult(true);
            }
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
