using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.RankingGroups.ValueObjects;

namespace LifeManager.Domain.Ranking
{
    /// <summary>
    /// A stored result with the member's current name and look. <see cref="MemberId"/> is null for someone who has left
    /// the group since.
    /// </summary>
    public record RankingWeekResultView(RankingWeekResult Result, RankingGroupMemberId? MemberId, string Name, PlayerAvatar Avatar, AchievementCode? Title);
}
