using LifeManager.Domain.MonthlySummaries.Errors;
using LifeManager.Domain.MonthlySummaries.ValueObjects;

namespace LifeManager.Domain.Test.MonthlySummaries
{
    public class TotalExpenseTests
    {
        [Theory]
        [InlineData(-1)]
        [InlineData(-10)]
        [InlineData(-100)]
        public void Create_ShouldReturnFailure_WhenValueIsNegative(int value)
        {
            var result = TotalExpense.Create(value);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.TotalExpenseNegative, result.Error);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(55)]
        public void Create_ShouldReturnTotalExpense_WhenValueIsValid(int value)
        {
            var result = TotalExpense.Create(value);

            Assert.True(result.IsSuccess);
            Assert.Equal(value, result.Value.Value);
        }

        [Fact]
        public void Create_ShouldAcceptNegativeZero_WhenAnEmptySumIsNegated()
        {
            var negativeZero = -Array.Empty<decimal>().Sum();

            var result = TotalExpense.Create(negativeZero);

            Assert.True(result.IsSuccess);
            Assert.Equal(0m, result.Value.Value);
        }

        [Fact]
        public void Zero_ShouldHaveZeroValue()
        {
            Assert.Equal(0m, TotalExpense.Zero.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const int value = 1;
            var valueOne = TotalExpense.Create(value).Value!;
            var valueTwo = TotalExpense.Create(value).Value!;

            Assert.True(valueOne.Equals(valueTwo));
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const int value = 1;
            var valueOne = TotalExpense.Create(value).Value!;
            var valueTwo = TotalExpense.Create(value).Value!;

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
