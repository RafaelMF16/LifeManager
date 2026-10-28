using LifeManager.Domain.Achievements.Enums;

namespace LifeManager.Application.Achievements.DTOs
{
    /// <summary>Body of <c>POST /api/Achievements/Seen</c>: the achievements the moment showed.</summary>
    public record MarkAchievementsSeenRequestDto(IReadOnlyList<AchievementCode>? Codes);
}
