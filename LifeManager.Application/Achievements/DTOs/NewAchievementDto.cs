using LifeManager.Domain.Achievements;
using LifeManager.Domain.Achievements.Enums;

namespace LifeManager.Application.Achievements.DTOs
{
    /// <summary>An unlock the player hasn't been shown yet, with the bonus it paid.</summary>
    public record NewAchievementDto(AchievementCode Code, AchievementTier Tier, int CoinsBonus, int XpBonus, DateTimeOffset UnlockedAt)
    {
        public static NewAchievementDto From(AchievementUnlock unlock)
        {
            var tier = AchievementCatalog.Tier(unlock.Code);
            var bonus = AchievementCatalog.Bonus(tier);

            return new(unlock.Code, tier, bonus.Coins, bonus.Xp, unlock.UnlockedAt);
        }
    }
}
