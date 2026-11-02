using LifeManager.Domain.RankingGroups.Errors;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.RankingGroups.Interfaces
{
    public interface IRankingGroupRepository
    {
        /// <summary>The groups <paramref name="userId"/> is in, by name.</summary>
        Task<IReadOnlyList<RankingGroupSummary>> GetSummariesAsync(UserId userId, CancellationToken cancellationToken);

        /// <summary>The group with its members, or null when <paramref name="userId"/> isn't one of them.</summary>
        Task<RankingGroupView?> GetForMemberAsync(RankingGroupId groupId, UserId userId, CancellationToken cancellationToken);

        /// <summary>The group with its members, for the jobs (no user to scope by).</summary>
        Task<RankingGroupView?> GetViewAsync(RankingGroupId groupId, CancellationToken cancellationToken);

        /// <summary>The members of the user's groups, to rank all of them at once.</summary>
        Task<IReadOnlyList<RankingGroupMember>> GetMembersOfUserGroupsAsync(UserId userId, CancellationToken cancellationToken);

        Task<RankingGroupPreview?> GetPreviewAsync(InviteCode inviteCode, UserId userId, CancellationToken cancellationToken);

        Task<bool> IsInviteCodeTakenAsync(InviteCode inviteCode, CancellationToken cancellationToken);

        /// <summary>
        /// Stores the group with its owner as the first member, in one transaction under the owner's player lock, so
        /// <see cref="RankingGroupMembership.CanCreate"/> sees an up-to-date count.
        /// </summary>
        Task<Result<RankingGroupId>> CreateAsync(RankingGroup group, DateOnly joinedOn, CancellationToken cancellationToken);

        /// <summary>
        /// Adds <paramref name="userId"/> to the group of <paramref name="inviteCode"/>, under the user's player lock and
        /// then the group's row lock (always in that order), checked by <see cref="RankingGroupMembership.CanJoin"/>.
        /// </summary>
        Task<Result<RankingGroupId>> JoinAsync(InviteCode inviteCode, UserId userId, DateTimeOffset joinedAt, DateOnly joinedOn, CancellationToken cancellationToken);

        /// <summary>
        /// Stores the group's name only, and only while <paramref name="ownerId"/> still owns it (a concurrent new code
        /// or a change of owner is never overwritten); false otherwise.
        /// </summary>
        Task<bool> SaveNameAsync(RankingGroup group, UserId ownerId, CancellationToken cancellationToken);

        /// <summary>Stores the group's invite code only, under the same owner check as <see cref="SaveNameAsync"/>.</summary>
        Task<bool> SaveInviteCodeAsync(RankingGroup group, UserId ownerId, CancellationToken cancellationToken);

        /// <summary>
        /// Removes <paramref name="userId"/> under the group's row lock, then passes the ownership on or deletes the
        /// emptied group (<see cref="RankingGroupMembership.OwnerAfterLeaving"/>). False when they weren't in it.
        /// </summary>
        Task<bool> LeaveAsync(RankingGroupId groupId, UserId userId, CancellationToken cancellationToken);

        /// <summary>
        /// Removes a member who isn't the owner, under the group's row lock and only while <paramref name="ownerId"/>
        /// still owns the group (<see cref="RankingGroupErrors.NotOwner"/>, or <see cref="RankingGroupErrors.NotFound"/>
        /// once it is gone); <see cref="RankingGroupErrors.MemberNotFound"/> when there is no such member.
        /// </summary>
        Task<Result> RemoveMemberAsync(RankingGroupId groupId, RankingGroupMemberId memberId, UserId ownerId, CancellationToken cancellationToken);
    }
}
