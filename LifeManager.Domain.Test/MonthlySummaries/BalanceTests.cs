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
        public void Create_ShouldReturnBalance_WhenValuesAreValid(int incomeValue, int expenseValue)
        {
            var totalExpense = TotalExpense.Create(expenseValue);
            var totalIncome = TotalIncome.Create(incomeValue);
            var balance = Balance.Create(totalIncome, totalExpense);
            var valueExpected = totalIncome.Value - totalExpense.Value;

            Assert.IsType<Balance>(balance);
            Assert.Equal(valueExpected, balance.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const int value = 1;
            var totalExpense = TotalExpense.Create(value);
            var totalIncome = TotalIncome.Create(value);
            var valueOne = Balance.Create(totalIncome, totalExpense);
            var valueTwo = Balance.Create(totalIncome, totalExpense);
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const int value = 1;
            var totalExpense = TotalExpense.Create(value);
            var totalIncome = TotalIncome.Create(value);
            var valueOne = Balance.Create(totalIncome, totalExpense);
            var valueTwo = Balance.Create(totalIncome, totalExpense);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}