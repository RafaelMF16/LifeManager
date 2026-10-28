namespace LifeManager.Domain.Achievements.Enums
{
    /// <summary>How the achievements are grouped for the player: each family shows its tiers together.</summary>
    public enum AchievementFamily
    {
        CheckIns = 1,
        Streak = 2,
        Level = 3,
        Redemptions = 4,
        CleanDays = 5,

        /// <summary>One-off feats: coming back from a knockout and a habit made automatic.</summary>
        Overcoming = 6
    }
}
