using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.RankingGroups;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.RankingGroups.DTOs
{
    public record RankingGroupRequestDto(string? Name);

    /// <param name="MyPosition">The user's place in the group this week (null until the ranking fills it in).</param>
    public record RankingGroupListItemDto(int Id, string Name, int MemberCount, bool IsOwner, int? MyPosition = null, int MyPoints = 0)
    {
        public static RankingGroupListItemDto From(RankingGroupSummary summary)
            => new(summary.Id.Value, summary.Name, summary.MemberCount, summary.IsOwner);
    }

    /// <summary>A member as the group sees them; <see cref="MemberId"/> is the only id, never the user's.</summary>
    public record RankingGroupMemberDto(int MemberId, string Name, PlayerAvatar Avatar, AchievementCode? Title, DateOnly JoinedOn, bool IsOwner, bool IsMe)
    {
        public static RankingGroupMemberDto From(RankingMemberView member, RankingGroup group, UserId me)
            => new(member.MemberId.Value, member.Name, member.Avatar, member.Title, member.JoinedOn, group.IsOwnedBy(member.UserId), member.UserId == me);
    }

    public record RankingGroupDetailsDto(int Id, string Name, string InviteCode, bool IsOwner, DateOnly CreatedOn, IReadOnlyList<RankingGroupMemberDto> Members)
    {
        public static RankingGroupDetailsDto From(RankingGroupView view, UserId me)
            => new(
                view.Group.Id!.Value,
                view.Group.Name.Value,
                view.Group.InviteCode.Value,
                view.Group.IsOwnedBy(me),
                view.Group.CreatedOn,
                [.. view.Members.Select(member => RankingGroupMemberDto.From(member, view.Group, me))]);
    }

    public record RankingGroupPreviewDto(string Name, string OwnerName, int MemberCount, bool AlreadyMember)
    {
        public static RankingGroupPreviewDto From(RankingGroupPreview preview)
            => new(preview.Name, preview.OwnerName, preview.MemberCount, preview.AlreadyMember);
    }
}
