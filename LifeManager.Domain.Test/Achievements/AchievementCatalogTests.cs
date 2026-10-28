using LifeManager.Domain.Achievements;
using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Habits;

namespace LifeManager.Domain.Test.Achievements
{
    public class AchievementCatalogTests
    {
        private static AchievementProgress Progress(
            int checkIns = 0,
            int longestStreakDays = 0,
            int level = 1,
            int redemptions = 0,
            int cleanDays = 0,
            int keptDaysAfterFirstKnockout = 0,
            int mostKeptDaysInOneHabit = 0)
            => new(checkIns, longestStreakDays, level, redemptions, cleanDays, keptDaysAfterFirstKnockout, mostKeptDaysInOneHabit);

        [Fact]
        public void All_ShouldListTheSixteenCodesInOrder()
        {
            Assert.Equal(Enum.GetValues<AchievementCode>(), AchievementCatalog.All);
            Assert.Equal(16, AchievementCatalog.All.Count);
        }

        [Theory]
        [InlineData(AchievementCode.CheckIns10, AchievementFamily.CheckIns, AchievementTier.Bronze, 10)]
        [InlineData(AchievementCode.CheckIns100, AchievementFamily.CheckIns, AchievementTier.Silver, 100)]
        [InlineData(AchievementCode.CheckIns500, AchievementFamily.CheckIns, AchievementTier.Gold, 500)]
        [InlineData(AchievementCode.Streak7, AchievementFamily.Streak, AchievementTier.Bronze, 7)]
        [InlineData(AchievementCode.Streak30, AchievementFamily.Streak, AchievementTier.Silver, 30)]
        [InlineData(AchievementCode.Streak100, AchievementFamily.Streak, AchievementTier.Gold, 100)]
        [InlineData(AchievementCode.Level5, AchievementFamily.Level, AchievementTier.Bronze, 5)]
        [InlineData(AchievementCode.Level10, AchievementFamily.Level, AchievementTier.Silver, 10)]
        [InlineData(AchievementCode.Level25, AchievementFamily.Level, AchievementTier.Gold, 25)]
        [InlineData(AchievementCode.Redemptions1, AchievementFamily.Redemptions, AchievementTier.Bronze, 1)]
        [InlineData(AchievementCode.Redemptions10, AchievementFamily.Redemptions, AchievementTier.Silver, 10)]
        [InlineData(AchievementCode.CleanDays10, AchievementFamily.CleanDays, AchievementTier.Bronze, 10)]
        [InlineData(AchievementCode.CleanDays50, AchievementFamily.CleanDays, AchievementTier.Silver, 50)]
        [InlineData(AchievementCode.CleanDays200, AchievementFamily.CleanDays, AchievementTier.Gold, 200)]
        [InlineData(AchievementCode.Comeback, AchievementFamily.Overcoming, AchievementTier.Silver, 10)]
        [InlineData(AchievementCode.AutomaticHabit, AchievementFamily.Overcoming, AchievementTier.Gold, 66)]
        public void Catalog_ShouldDescribeEveryAchievement(AchievementCode code, AchievementFamily family, AchievementTier tier, int threshold)
        {
            Assert.Equal(family, AchievementCatalog.Family(code));
            Assert.Equal(tier, AchievementCatalog.Tier(code));
            Assert.Equal(threshold, AchievementCatalog.Threshold(code));
        }

        [Theory]
        [InlineData(AchievementTier.Bronze, 25, 50)]
        [InlineData(AchievementTier.Silver, 100, 150)]
        [InlineData(AchievementTier.Gold, 300, 400)]
        public void Bonus_ShouldPayCoinsAndXpByTier(AchievementTier tier, int coins, int xp)
        {
            Assert.Equal(new GameDelta(coins, xp, 0), AchievementCatalog.Bonus(tier));
        }

        [Fact]
        public void NewlyUnlocked_ShouldBeEmpty_JustBelowTheThreshold()
        {
            Assert.Empty(AchievementCatalog.NewlyUnlocked(Progress(checkIns: 9), []));
        }

        [Fact]
        public void NewlyUnlocked_ShouldUnlockBronze_AtTheThreshold()
        {
            Assert.Equal([AchievementCode.CheckIns10], AchievementCatalog.NewlyUnlocked(Progress(checkIns: 10), []));
        }

        [Fact]
        public void NewlyUnlocked_ShouldUnlockEveryTierPassed_InCatalogOrder()
        {
            var unlocked = AchievementCatalog.NewlyUnlocked(Progress(checkIns: 120, longestStreakDays: 8, level: 3), []);

            Assert.Equal([AchievementCode.CheckIns10, AchievementCode.CheckIns100, AchievementCode.Streak7], unlocked);
        }

        [Fact]
        public void NewlyUnlocked_ShouldSkipTheOnesAlreadyUnlocked()
        {
            var unlocked = AchievementCatalog.NewlyUnlocked(Progress(checkIns: 120), [AchievementCode.CheckIns10]);

            Assert.Equal([AchievementCode.CheckIns100], unlocked);
        }

        [Fact]
        public void NewlyUnlocked_ShouldReadEachFamilyFromItsOwnNumber()
        {
            var progress = Progress(level: 5, redemptions: 1, cleanDays: 10, keptDaysAfterFirstKnockout: 10, mostKeptDaysInOneHabit: 66);

            Assert.Equal(
                [AchievementCode.Level5, AchievementCode.Redemptions1, AchievementCode.CleanDays10, AchievementCode.Comeback, AchievementCode.AutomaticHabit],
                AchievementCatalog.NewlyUnlocked(progress, []));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(37, 37)]
        [InlineData(100, 100)]
        [InlineData(250, 100)]
        public void Progress_ShouldBeCappedAtTheThreshold(int checkIns, int expected)
        {
            Assert.Equal(expected, AchievementCatalog.Progress(AchievementCode.CheckIns100, Progress(checkIns: checkIns)));
        }
    }
}
