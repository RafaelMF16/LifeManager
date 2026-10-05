using LifeManager.Domain.FinanceDashboard;

namespace LifeManager.Domain.Test.FinanceDashboard
{
    public class DashboardTotalsTests
    {
        [Theory]
        [InlineData(5000, 3500, 1000, 500)]
        [InlineData(100, 0, 300, -200)]
        [InlineData(0, 0, 0, 0)]
        public void Balance_ShouldBeIncomeMinusExpenseMinusInvestment(decimal income, decimal expense, decimal investment, decimal expectedBalance)
        {
            Assert.Equal(expectedBalance, new DashboardTotals(income, expense, investment).Balance);
        }

        [Theory]
        [InlineData(112, 100, 0.12)]
        [InlineData(50, 100, -0.5)]
        [InlineData(100, 100, 0)]
        [InlineData(-50, -100, 0.5)]
        [InlineData(-150, -100, -0.5)]
        [InlineData(1, 3, -0.6667)]
        public void ChangeRatio_ShouldCompareWithTheSizeOfThePreviousValue(decimal current, decimal previous, decimal expected)
        {
            Assert.Equal(expected, DashboardTotals.ChangeRatio(current, previous));
        }

        [Fact]
        public void ChangeRatio_ShouldBeNull_WhenPreviousIsZero()
        {
            Assert.Null(DashboardTotals.ChangeRatio(100, 0));
        }

        [Theory]
        [InlineData(25, 100, 0.25)]
        [InlineData(1, 3, 0.3333)]
        [InlineData(0, 0, 0)]
        public void Share_ShouldBeThePartOfTheTotal(decimal amount, decimal total, decimal expected)
        {
            Assert.Equal(expected, DashboardTotals.Share(amount, total));
        }
    }
}
