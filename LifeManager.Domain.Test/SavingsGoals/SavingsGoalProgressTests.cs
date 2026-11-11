using LifeManager.Domain.SavingsGoals;
using LifeManager.Domain.SavingsGoals.Enums;
using LifeManager.Domain.Shared.ValueObjects;

namespace LifeManager.Domain.Test.SavingsGoals
{
    public class SavingsGoalProgressTests
    {
        private static readonly YearMonth Current = Month("2026-11");

        private static YearMonth Month(string value)
        {
            Assert.True(YearMonth.TryParse(value, out var month));
            return month;
        }

        [Fact]
        public void Calculate_ShouldSumInitialAndLinked_AndSplitWhatIsLeft()
        {
            // R$ 5.000 by Dec/2027: Nov/2026..Dec/2027 is 14 months.
            var progress = SavingsGoalProgress.Calculate(5000m, 300m, 200m, Month("2027-12"), Current, recentSaved: 0m);

            Assert.Equal(500m, progress.Saved);
            Assert.Equal(0.1m, progress.Ratio);
            Assert.Equal(4500m, progress.Remaining);
            Assert.Equal(14, progress.MonthsLeft);
            Assert.Equal(321.43m, progress.MonthlyNeeded);
            Assert.Equal(SavingsGoalPace.Behind, progress.Pace);
        }

        [Fact]
        public void Calculate_ShouldRoundTheMonthlyNeededUpToTheCent()
        {
            var progress = SavingsGoalProgress.Calculate(100m, 0m, 0m, Month("2027-01"), Current, recentSaved: 0m);

            Assert.Equal(3, progress.MonthsLeft);
            Assert.Equal(33.34m, progress.MonthlyNeeded);
        }

        [Fact]
        public void Calculate_ShouldAskForEverythingLeft_WhenTheDeadlineIsThisMonth()
        {
            var progress = SavingsGoalProgress.Calculate(1000m, 0m, 400m, Current, Current, recentSaved: 400m);

            Assert.Equal(1, progress.MonthsLeft);
            Assert.Equal(600m, progress.MonthlyNeeded);
            Assert.Equal(SavingsGoalPace.Behind, progress.Pace);
        }

        [Fact]
        public void Calculate_ShouldBeOnTrack_WhenTheRecentAverageCoversTheMonthlyNeeded()
        {
            // 1.200 left over 12 months = 100/month; 300 saved in the last 3 months = 100/month.
            var progress = SavingsGoalProgress.Calculate(1500m, 0m, 300m, Month("2027-10"), Current, recentSaved: 300m);

            Assert.Equal(100m, progress.MonthlyNeeded);
            Assert.Equal(SavingsGoalPace.OnTrack, progress.Pace);
        }

        [Fact]
        public void Calculate_ShouldBeBehind_WhenTheRecentAverageFallsShort()
        {
            var progress = SavingsGoalProgress.Calculate(1500m, 0m, 300m, Month("2027-10"), Current, recentSaved: 299.99m);

            Assert.Equal(SavingsGoalPace.Behind, progress.Pace);
        }

        [Fact]
        public void Calculate_ShouldHaveNoDeadlinePace_WithoutATargetMonth()
        {
            var progress = SavingsGoalProgress.Calculate(20000m, 1000m, 0m, null, Current, recentSaved: 0m);

            Assert.Null(progress.MonthsLeft);
            Assert.Null(progress.MonthlyNeeded);
            Assert.Equal(SavingsGoalPace.NoDeadline, progress.Pace);
        }

        [Fact]
        public void Calculate_ShouldBeDone_WhenTheTargetIsReached()
        {
            var progress = SavingsGoalProgress.Calculate(1000m, 500m, 600m, Month("2027-12"), Current, recentSaved: 0m);

            Assert.Equal(1100m, progress.Saved);
            Assert.Equal(1.1m, progress.Ratio);
            Assert.Equal(0m, progress.Remaining);
            Assert.Equal(0m, progress.MonthlyNeeded);
            Assert.Equal(SavingsGoalPace.Done, progress.Pace);
            Assert.True(progress.IsReached);
        }

        [Fact]
        public void Calculate_ShouldBeDone_WithoutDeadlineToo()
        {
            var progress = SavingsGoalProgress.Calculate(1000m, 1000m, 0m, null, Current, recentSaved: 0m);

            Assert.Equal(SavingsGoalPace.Done, progress.Pace);
        }

        [Fact]
        public void Calculate_ShouldAskForEverythingLeft_WhenTheDeadlinePassed()
        {
            var progress = SavingsGoalProgress.Calculate(1000m, 0m, 100m, Month("2026-08"), Current, recentSaved: 900m);

            Assert.Equal(-2, progress.MonthsLeft);
            Assert.Equal(900m, progress.MonthlyNeeded);
            Assert.Equal(SavingsGoalPace.Behind, progress.Pace);
        }

        [Fact]
        public void Reaches_ShouldCompareSavedWithTheTarget()
        {
            Assert.True(SavingsGoalProgress.Reaches(1000m, 400m, 600m));
            Assert.False(SavingsGoalProgress.Reaches(1000m, 400m, 599.99m));
        }
    }
}
