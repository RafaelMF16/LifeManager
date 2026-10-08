using LifeManager.Domain.Rewards;

namespace LifeManager.Domain.Test.Rewards
{
    public class RewardPaceTests
    {
        private static readonly DateOnly Today = new(2026, 10, 8);

        [Fact]
        public void WindowStart_ShouldIncludeToday()
        {
            Assert.Equal(Today.AddDays(-(RewardPace.WindowDays - 1)), RewardPace.WindowStart(Today));
        }

        [Fact]
        public void AverageDailyCoins_ShouldBeZero_WithoutEarnings()
        {
            Assert.Equal(0m, RewardPace.AverageDailyCoins(0, null, Today));
            Assert.Equal(0m, RewardPace.AverageDailyCoins(-10, Today, Today));
        }

        [Fact]
        public void AverageDailyCoins_ShouldAverageOverTheWholeWindow_ForAnEstablishedPlayer()
        {
            var average = RewardPace.AverageDailyCoins(140, RewardPace.WindowStart(Today).AddDays(-30), Today);

            Assert.Equal(10m, average);
        }

        [Fact]
        public void AverageDailyCoins_ShouldAverageFromTheFirstEarningDay_ForANewPlayer()
        {
            // Started 3 days ago (3 days counting today): 45 coins over 3 days.
            var average = RewardPace.AverageDailyCoins(45, Today.AddDays(-2), Today);

            Assert.Equal(15m, average);
        }

        [Fact]
        public void AverageDailyCoins_ShouldRoundToOneDecimal()
        {
            var average = RewardPace.AverageDailyCoins(10, Today.AddDays(-2), Today);

            Assert.Equal(3.3m, average);
        }
    }
}
