using LifeManager.Domain.MonthlySummaries.Errors;
using LifeManager.Domain.MonthlySummaries.ValueObjects;

namespace LifeManager.Domain.Test.MonthlySummaries
{
    public class TotalIncomeTests
    {
        [Theory]
        [InlineData(-1)]
        [InlineData(-10)]
        [InlineData(-100)]
        public void Create_ShouldReturnFailure_WhenValueIsNegative(int value)
        {
            var result = TotalIncome.Create(value);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.TotalIncomeNegative, result.Error);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(55)]
        public void Create_ShouldReturnTotalIncome_WhenValueIsValid(int value)
        {
            var result = TotalIncome.Create(value);

            Assert.True(result.IsSuccess);
            Assert.Equal(value, result.Value.Value);
        }

        [Fact]
        public void Zero_ShouldHaveZeroValue()
        {
            Assert.Equal(0m, TotalIncome.Zero.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const int value = 1;
            var valueOne = TotalIncome.Create(value).Value!;
            var valueTwo = TotalIncome.Create(value).Value!;

            Assert.True(valueOne.Equals(valueTwo));
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const int value = 1;
            var valueOne = TotalIncome.Create(value).Value!;
            var valueTwo = TotalIncome.Create(value).Value!;

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
