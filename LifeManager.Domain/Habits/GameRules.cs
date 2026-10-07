using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Habits
{
    /// <summary>What a habit is worth by difficulty: coins and XP when done, HP lost when missed.</summary>
    public record HabitReward(int Coins, int Xp, int Damage);

    /// <summary>
    /// Every number of the habits game, in one place so it can be tuned without touching the rest. Pure functions only:
    /// the entities and services call these, they never hardcode a value.
    /// </summary>
    public static class GameRules
    {
        public const int MaxHp = 100;
        public const int StartingLevel = 1;

        /// <summary>HP recovered by every completed habit.</summary>
        public const int HealPerCompletion = 1;

        /// <summary>Share of the coins lost when HP reaches 0 (rounded down, so the player never loses more).</summary>
        public const decimal KnockoutCoinLossRate = 0.2m;

        /// <summary>A streak freeze is earned every this many streak days.</summary>
        public const int FreezeEvery = 7;
        public const int MaxStreakFreezes = 2;

        /// <summary>Coin bonus per <see cref="FreezeEvery"/> streak days, capped at <see cref="MaxStreakBonusSteps"/> steps (+50%).</summary>
        public const decimal StreakBonusStep = 0.1m;
        public const int MaxStreakBonusSteps = 5;
        public const int StreakBonusEvery = 7;

        private const int XpPerLevelFactor = 100;

        private static readonly IReadOnlyDictionary<int, int> MilestoneBonuses = new Dictionary<int, int>
        {
            [7] = 25,
            [30] = 100,
            [66] = 250,
            [100] = 500
        };

        public static HabitReward Reward(HabitDifficulty difficulty)
            => difficulty switch
            {
                HabitDifficulty.Easy => new HabitReward(Coins: 5, Xp: 10, Damage: 5),
                HabitDifficulty.Medium => new HabitReward(Coins: 10, Xp: 20, Damage: 8),
                HabitDifficulty.Hard => new HabitReward(Coins: 20, Xp: 40, Damage: 12),
                _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Unknown habit difficulty")
            };

        /// <summary>XP needed to go from <paramref name="level"/> to the next one.</summary>
        public static int XpToNextLevel(int level)
            => XpPerLevelFactor * level;

        /// <summary>Total XP at which <paramref name="level"/> starts: the sum of every previous level's requirement.</summary>
        public static int TotalXpForLevel(int level)
            => XpPerLevelFactor * (level - 1) * level / 2;

        public static int LevelForTotalXp(int totalXp)
        {
            var level = StartingLevel;
            while (totalXp >= TotalXpForLevel(level + 1))
                level++;

            return level;
        }

        /// <summary>Coins for a completion with the streak bonus: +10% every 7 streak days, up to +50%.</summary>
        public static int CoinsWithStreakBonus(int baseCoins, int streak)
        {
            var steps = Math.Min(Math.Max(streak, 0) / StreakBonusEvery, MaxStreakBonusSteps);
            var coins = baseCoins * (1 + StreakBonusStep * steps);

            return (int)Math.Round(coins, MidpointRounding.AwayFromZero);
        }

        /// <summary>One-off coins for reaching a streak milestone (7, 30, 66 and 100 days); 0 on any other day.</summary>
        public static int MilestoneBonus(int streak)
            => MilestoneBonuses.TryGetValue(streak, out var bonus) ? bonus : 0;

        public static int KnockoutCoinLoss(int coins)
            => (int)Math.Floor(Math.Max(coins, 0) * KnockoutCoinLossRate);
    }
}
