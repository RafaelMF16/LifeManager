namespace LifeManager.Application.Achievements.DTOs
{
    /// <summary>The whole catalog, in its order, with the player's progress.</summary>
    public record AchievementsResponseDto(IReadOnlyList<AchievementDto> Achievements, int UnlockedCount, int Total);
}
