using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.RankingGroups.Errors
{
    public static class RankingGroupErrors
    {
        // A group the user isn't in answers like a missing one, so its existence never leaks.
        public static readonly Error NotFound = Error.NotFound("RankingGroups.NotFound", "Ranking group not found");
        public static readonly Error InviteNotFound = Error.NotFound("RankingGroups.InviteNotFound", "Invite code not found");
        public static readonly Error MemberNotFound = Error.NotFound("RankingGroups.MemberNotFound", "Group member not found");

        public static readonly Error NameIsNullOrWhiteSpace = Error.Validation("RankingGroups.NameIsNullOrWhiteSpace", "Group name is required");
        public static readonly Error NameTooLong = Error.Validation("RankingGroups.NameTooLong", "Group name cannot be longer than 40 characters");
        public static readonly Error CannotRemoveSelf = Error.Validation("RankingGroups.CannotRemoveSelf", "The owner leaves the group instead of removing themselves");

        public static readonly Error TooManyGroups = Error.Conflict("RankingGroups.TooManyGroups", "A player can be in at most 5 ranking groups");
        public static readonly Error GroupFull = Error.Conflict("RankingGroups.GroupFull", "This group already has the maximum number of members");
        public static readonly Error AlreadyMember = Error.Conflict("RankingGroups.AlreadyMember", "You are already in this group");
        public static readonly Error NotOwner = Error.Conflict("RankingGroups.NotOwner", "Only the group's owner can do this");
    }
}
