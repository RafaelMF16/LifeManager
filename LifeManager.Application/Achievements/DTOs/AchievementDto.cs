using LifeManager.Domain.Achievements;
using LifeManager.Domain.Achievements.Enums;

namespace LifeManager.Application.Achievements.DTOs
{
    /// <summary>One achievement and where the player stands with it.</summary>
    /// <param name="Progress">Towards <paramref name="Threshold"/>, never past it; the threshold once unlocked.</param>
    /// <param name="UnlockedAt">Null while locked.</param>
    public record AchievementDto(
        AchievementCode Code,
        AchievementFamily Family,
        AchievementTier Tier,
        int Threshold,
        int Progress,
        int CoinsBonus,
        int XpBonus,
        DateTimeOffset? UnlockedAt)
    {
        public static AchievementDto From(AchievementCode code, AchievementProgress progress, AchievementUnlock? unlock)
        {
            var tier = AchievementCatalog.Tier(code);
            var bonus = AchievementCatalog.Bonus(tier);
            var threshold = AchievementCatalog.Threshold(code);

            return new(
                code,
                AchievementCatalog.Family(code),
                tier,
                threshold,
                unlock is null ? AchievementCatalog.Progress(code, progress) : threshold,
                bonus.Coins,
                bonus.Xp,
                unlock?.UnlockedAt);
        }
    }
}
