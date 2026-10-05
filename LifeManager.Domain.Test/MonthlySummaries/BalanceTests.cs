using LifeManager.Domain.MonthlySummaries.ValueObjects;

namespace LifeManager.Domain.Test.MonthlySummaries
{
    public class BalanceTests
    {
        [Theory]
        [InlineData(100, 50, 0)]
        [InlineData(100, 100, 0)]
        [InlineData(100, 200, 0)]
        [InlineData(0, 0, 0)]
        [InlineData(1000, 300, 200)]
        [InlineData(100, 0, 300)]
        public void Create_ShouldReturnIncomeMinusExpenseMinusInvestment(int incomeValue, int expenseValue, int investmentValue)
        {
            var totalIncome = TotalIncome.Create(incomeValue).Value!;
            var totalExpense = TotalExpense.Create(expenseValue).Value!;
            var totalInvestment = TotalInvestment.Create(investmentValue).Value!;

            var balance = Balance.Create(totalIncome, totalExpense, totalInvestment);

            Assert.Equal(incomeValue - expenseValue - investmentValue, balance.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            var totalIncome = TotalIncome.Create(1).Value!;
            var totalExpense = TotalExpense.Create(1).Value!;
            var valueOne = Balance.Create(totalIncome, totalExpense, TotalInvestment.Zero);
            var valueTwo = Balance.Create(totalIncome, totalExpense, TotalInvestment.Zero);

            Assert.True(valueOne.Equals(valueTwo));
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            var totalIncome = TotalIncome.Create(1).Value!;
            var totalExpense = TotalExpense.Create(1).Value!;
            var valueOne = Balance.Create(totalIncome, totalExpense, TotalInvestment.Zero);
            var valueTwo = Balance.Create(totalIncome, totalExpense, TotalInvestment.Zero);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
