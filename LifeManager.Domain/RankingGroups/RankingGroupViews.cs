using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.RankingGroups
{
    /// <summary>One of the user's groups, for their list.</summary>
    public record RankingGroupSummary(RankingGroupId Id, string Name, int MemberCount, bool IsOwner, DateOnly CreatedOn);

    /// <summary>
    /// A member as the group sees them: the account's name and the player's look (a player without a profile yet shows
    /// the default avatar). <see cref="UserId"/> stays on the server: responses use <see cref="MemberId"/>.
    /// </summary>
    public record RankingMemberView(
        RankingGroupMemberId MemberId,
        UserId UserId,
        string Name,
        PlayerAvatar Avatar,
        AchievementCode? Title,
        DateTimeOffset JoinedAt,
        DateOnly JoinedOn);

    /// <summary>A group with its members, oldest first.</summary>
    public record RankingGroupView(RankingGroup Group, IReadOnlyList<RankingMemberView> Members);

    /// <summary>What an invite shows before joining.</summary>
    public record RankingGroupPreview(RankingGroupId Id, string Name, string OwnerName, int MemberCount, bool AlreadyMember);
}
