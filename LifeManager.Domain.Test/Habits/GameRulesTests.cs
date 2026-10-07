using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Test.Habits
{
    public class GameRulesTests
    {
        [Theory]
        [InlineData(HabitDifficulty.Easy, 5, 10, 5)]
        [InlineData(HabitDifficulty.Medium, 10, 20, 8)]
        [InlineData(HabitDifficulty.Hard, 20, 40, 12)]
        public void Reward_ShouldReturnCoinsXpAndDamage_WhenDifficultyIsDefined(HabitDifficulty difficulty, int coins, int xp, int damage)
        {
            var reward = GameRules.Reward(difficulty);

            Assert.Equal(new HabitReward(coins, xp, damage), reward);
        }

        [Fact]
        public void Reward_ShouldThrow_WhenDifficultyIsNotDefined()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GameRules.Reward((HabitDifficulty)99));
        }

        [Theory]
        [InlineData(1, 100)]
        [InlineData(2, 200)]
        [InlineData(10, 1000)]
        public void XpToNextLevel_ShouldBeOneHundredTimesTheLevel(int level, int expected)
        {
            Assert.Equal(expected, GameRules.XpToNextLevel(level));
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(2, 100)]
        [InlineData(3, 300)]
        [InlineData(4, 600)]
        public void TotalXpForLevel_ShouldSumThePreviousLevelsRequirements(int level, int expected)
        {
            Assert.Equal(expected, GameRules.TotalXpForLevel(level));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(99, 1)]
        [InlineData(100, 2)]
        [InlineData(299, 2)]
        [InlineData(300, 3)]
        [InlineData(600, 4)]
        public void LevelForTotalXp_ShouldReturnTheLevelReached_WhenAtOrAroundTheThresholds(int totalXp, int expected)
        {
            Assert.Equal(expected, GameRules.LevelForTotalXp(totalXp));
        }

        [Theory]
        [InlineData(5, 0, 5)]
        [InlineData(5, 6, 5)]
        [InlineData(5, 7, 6)]
        [InlineData(5, 13, 6)]
        [InlineData(5, 14, 6)]
        [InlineData(10, 21, 13)]
        [InlineData(5, 35, 8)]
        [InlineData(10, 35, 15)]
        [InlineData(10, 70, 15)]
        [InlineData(20, 7, 22)]
        public void CoinsWithStreakBonus_ShouldAddTenPercentEverySevenDays_UpToFiftyPercent(int baseCoins, int streak, int expected)
        {
            Assert.Equal(expected, GameRules.CoinsWithStreakBonus(baseCoins, streak));
        }

        [Fact]
        public void CoinsWithStreakBonus_ShouldReturnBaseCoins_WhenStreakIsNegative()
        {
            Assert.Equal(10, GameRules.CoinsWithStreakBonus(10, -7));
        }

        [Theory]
        [InlineData(7, 25)]
        [InlineData(30, 100)]
        [InlineData(66, 250)]
        [InlineData(100, 500)]
        public void MilestoneBonus_ShouldReturnBonus_WhenStreakIsAMilestone(int streak, int expected)
        {
            Assert.Equal(expected, GameRules.MilestoneBonus(streak));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(6)]
        [InlineData(8)]
        [InlineData(14)]
        [InlineData(65)]
        [InlineData(101)]
        public void MilestoneBonus_ShouldReturnZero_WhenStreakIsNotAMilestone(int streak)
        {
            Assert.Equal(0, GameRules.MilestoneBonus(streak));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(4, 0)]
        [InlineData(5, 1)]
        [InlineData(99, 19)]
        [InlineData(100, 20)]
        public void KnockoutCoinLoss_ShouldBeTwentyPercentRoundedDown(int coins, int expected)
        {
            Assert.Equal(expected, GameRules.KnockoutCoinLoss(coins));
        }
    }
}
