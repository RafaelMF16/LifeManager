using LifeManager.Domain.MonthlySummaries.ValueObjects;

namespace LifeManager.Domain.Test.MonthlySummaries
{
    public class BalanceTests
    {
        [Theory]
        [InlineData(100, 50)]
        [InlineData(100, 100)]
        [InlineData(100, 200)]
        [InlineData(0, 0)]
        public void Create_ShouldReturnIncomeMinusExpense(int incomeValue, int expenseValue)
        {
            var totalIncome = TotalIncome.Create(incomeValue).Value!;
            var totalExpense = TotalExpense.Create(expenseValue).Value!;

            var balance = Balance.Create(totalIncome, totalExpense);

            Assert.Equal(incomeValue - expenseValue, balance.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            var totalIncome = TotalIncome.Create(1).Value!;
            var totalExpense = TotalExpense.Create(1).Value!;
            var valueOne = Balance.Create(totalIncome, totalExpense);
            var valueTwo = Balance.Create(totalIncome, totalExpense);

            Assert.True(valueOne.Equals(valueTwo));
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            var totalIncome = TotalIncome.Create(1).Value!;
            var totalExpense = TotalExpense.Create(1).Value!;
            var valueOne = Balance.Create(totalIncome, totalExpense);
            var valueTwo = Balance.Create(totalIncome, totalExpense);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
