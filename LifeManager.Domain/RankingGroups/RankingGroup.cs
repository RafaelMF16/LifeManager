using LifeManager.Domain.RankingGroups.Errors;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.RankingGroups
{
    /// <summary>
    /// People who compete in the same weekly ranking. Its creator is the first owner and member; anyone in it can invite
    /// with the <see cref="InviteCode"/>, and only the owner manages it.
    /// </summary>
    public class RankingGroup
    {
        public RankingGroupId? Id { get; private set; }
        public RankingGroupName Name { get; private set; }
        public UserId OwnerUserId { get; private set; }
        public InviteCode InviteCode { get; private set; }
        public DateTimeOffset CreatedAt { get; }

        /// <summary>The game day it was created: its first ranking week is that day's week.</summary>
        public DateOnly CreatedOn { get; }

        private RankingGroup(RankingGroupName name, UserId ownerUserId, InviteCode inviteCode, DateTimeOffset createdAt, DateOnly createdOn)
        {
            Name = name;
            OwnerUserId = ownerUserId;
            InviteCode = inviteCode;
            CreatedAt = createdAt;
            CreatedOn = createdOn;
        }

        public static Result<RankingGroup> Create(string? name, UserId ownerUserId, InviteCode inviteCode, DateTimeOffset createdAt, DateOnly createdOn)
            => RankingGroupName.Create(name)
                .Map(groupName => new RankingGroup(groupName, ownerUserId, inviteCode, createdAt, createdOn));

        public bool IsOwnedBy(UserId userId)
            => OwnerUserId == userId;

        public Result Rename(string? name, UserId by)
        {
            if (!IsOwnedBy(by))
                return RankingGroupErrors.NotOwner;

            var groupName = RankingGroupName.Create(name);
            if (!groupName.IsSuccess)
                return groupName.Error!;

            Name = groupName.Value!;
            return Result.Success();
        }

        /// <summary>Replaces the code; the old one stops working at once.</summary>
        public Result ChangeInviteCode(InviteCode inviteCode, UserId by)
        {
            if (!IsOwnedBy(by))
                return RankingGroupErrors.NotOwner;

            InviteCode = inviteCode;
            return Result.Success();
        }

        public void AssignId(int id)
        {
            Id = new RankingGroupId(id);
        }

        internal static RankingGroup FromPersistence(int id, string name, int idOwner, string inviteCode, DateTimeOffset createdAt, DateOnly createdOn)
        {
            var group = new RankingGroup(
                RankingGroupName.FromPersistence(name), new UserId(idOwner), InviteCode.FromPersistence(inviteCode), createdAt, createdOn);
            group.AssignId(id);

            return group;
        }

        public override bool Equals(object? obj)
            => obj is RankingGroup other && Id is not null && Id.Equals(other.Id);

        public override int GetHashCode()
            => Id?.GetHashCode() ?? base.GetHashCode();
    }
}
