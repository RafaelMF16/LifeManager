using LifeManager.Domain.RankingGroups.Errors;
using LifeManager.Domain.Ranking;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.RankingGroups
{
    /// <summary>Who may get into a group and what happens when someone leaves. Pure: the repositories call it under their locks.</summary>
    public static class RankingGroupMembership
    {
        /// <param name="userGroupCount">Groups the user is already in.</param>
        public static Result CanCreate(int userGroupCount)
            => userGroupCount >= RankingRules.MaxGroupsPerUser ? RankingGroupErrors.TooManyGroups : Result.Success();

        public static Result CanJoin(int userGroupCount, int memberCount, bool alreadyMember)
        {
            if (alreadyMember)
                return RankingGroupErrors.AlreadyMember;

            if (memberCount >= RankingRules.MaxMembersPerGroup)
                return RankingGroupErrors.GroupFull;

            return CanCreate(userGroupCount);
        }

        /// <summary>
        /// The owner after <paramref name="leaving"/> leaves: the same owner when someone else leaves; when the owner
        /// leaves, the longest-standing member (by join time, then id). Null when nobody is left: the group goes away.
        /// </summary>
        public static UserId? OwnerAfterLeaving(RankingGroup group, IReadOnlyCollection<RankingGroupMember> members, UserId leaving)
        {
            var remaining = members.Where(member => member.UserId != leaving).ToList();
            if (remaining.Count == 0)
                return null;

            if (!group.IsOwnedBy(leaving))
                return group.OwnerUserId;

            return remaining
                .OrderBy(member => member.JoinedAt)
                .ThenBy(member => member.Id?.Value ?? int.MaxValue)
                .First()
                .UserId;
        }
    }
}
