using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Habits;

namespace LifeManager.Domain.Achievements
{
    /// <summary>
    /// The fixed achievements and their rules (numbers in <see cref="GameRules"/>). Pure: each one is a threshold on a
    /// number of <see cref="AchievementProgress"/>, so the same history always unlocks the same achievements.
    /// </summary>
    public static class AchievementCatalog
    {
        private record Definition(AchievementFamily Family, AchievementTier Tier, int Threshold, Func<AchievementProgress, int> Count);

        private static readonly IReadOnlyDictionary<AchievementCode, Definition> Definitions = new Dictionary<AchievementCode, Definition>
        {
            [AchievementCode.CheckIns10] = Tiered(AchievementFamily.CheckIns, GameRules.CheckInAchievements, 0, progress => progress.CheckIns),
            [AchievementCode.CheckIns100] = Tiered(AchievementFamily.CheckIns, GameRules.CheckInAchievements, 1, progress => progress.CheckIns),
            [AchievementCode.CheckIns500] = Tiered(AchievementFamily.CheckIns, GameRules.CheckInAchievements, 2, progress => progress.CheckIns),
            [AchievementCode.Streak7] = Tiered(AchievementFamily.Streak, GameRules.StreakAchievements, 0, progress => progress.LongestStreakDays),
            [AchievementCode.Streak30] = Tiered(AchievementFamily.Streak, GameRules.StreakAchievements, 1, progress => progress.LongestStreakDays),
            [AchievementCode.Streak100] = Tiered(AchievementFamily.Streak, GameRules.StreakAchievements, 2, progress => progress.LongestStreakDays),
            [AchievementCode.Level5] = Tiered(AchievementFamily.Level, GameRules.LevelAchievements, 0, progress => progress.Level),
            [AchievementCode.Level10] = Tiered(AchievementFamily.Level, GameRules.LevelAchievements, 1, progress => progress.Level),
            [AchievementCode.Level25] = Tiered(AchievementFamily.Level, GameRules.LevelAchievements, 2, progress => progress.Level),
            [AchievementCode.Redemptions1] = Tiered(AchievementFamily.Redemptions, GameRules.RedemptionAchievements, 0, progress => progress.Redemptions),
            [AchievementCode.Redemptions10] = Tiered(AchievementFamily.Redemptions, GameRules.RedemptionAchievements, 1, progress => progress.Redemptions),
            [AchievementCode.CleanDays10] = Tiered(AchievementFamily.CleanDays, GameRules.CleanDayAchievements, 0, progress => progress.CleanDays),
            [AchievementCode.CleanDays50] = Tiered(AchievementFamily.CleanDays, GameRules.CleanDayAchievements, 1, progress => progress.CleanDays),
            [AchievementCode.CleanDays200] = Tiered(AchievementFamily.CleanDays, GameRules.CleanDayAchievements, 2, progress => progress.CleanDays),
            [AchievementCode.Comeback] = new(AchievementFamily.Overcoming, AchievementTier.Silver, GameRules.ComebackKeptDays, progress => progress.KeptDaysAfterFirstKnockout),
            [AchievementCode.AutomaticHabit] = new(AchievementFamily.Overcoming, AchievementTier.Gold, GameRules.AutomaticHabitKeptDays, progress => progress.MostKeptDaysInOneHabit)
        };

        public static readonly IReadOnlyList<AchievementCode> All = [.. Definitions.Keys.Order()];

        public static AchievementFamily Family(AchievementCode code)
            => Get(code).Family;

        public static AchievementTier Tier(AchievementCode code)
            => Get(code).Tier;

        public static int Threshold(AchievementCode code)
            => Get(code).Threshold;

        /// <summary>How far the player is towards <paramref name="code"/>, never past its threshold.</summary>
        public static int Progress(AchievementCode code, AchievementProgress progress)
        {
            var definition = Get(code);
            return Math.Clamp(definition.Count(progress), 0, definition.Threshold);
        }

        /// <summary>The achievements <paramref name="progress"/> reaches that aren't in <paramref name="unlocked"/>, in catalog order.</summary>
        public static IReadOnlyList<AchievementCode> NewlyUnlocked(AchievementProgress progress, IReadOnlyCollection<AchievementCode> unlocked)
            => [.. All.Where(code => !unlocked.Contains(code) && Get(code).Count(progress) >= Get(code).Threshold)];

        /// <summary>The one-off coins and XP an achievement of <paramref name="tier"/> pays.</summary>
        public static GameDelta Bonus(AchievementTier tier)
            => tier switch
            {
                AchievementTier.Bronze => new GameDelta(GameRules.BronzeAchievementCoins, GameRules.BronzeAchievementXp, 0),
                AchievementTier.Silver => new GameDelta(GameRules.SilverAchievementCoins, GameRules.SilverAchievementXp, 0),
                AchievementTier.Gold => new GameDelta(GameRules.GoldAchievementCoins, GameRules.GoldAchievementXp, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown achievement tier")
            };

        private static Definition Tiered(AchievementFamily family, IReadOnlyList<int> thresholds, int index, Func<AchievementProgress, int> count)
            => new(family, (AchievementTier)(index + 1), thresholds[index], count);

        private static Definition Get(AchievementCode code)
            => Definitions.TryGetValue(code, out var definition)
                ? definition
                : throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown achievement");
    }
}
