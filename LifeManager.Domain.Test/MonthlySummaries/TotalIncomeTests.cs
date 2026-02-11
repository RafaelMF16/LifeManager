using LifeManager.Domain.Exceptions;
using LifeManager.Domain.MonthlySummaries.ValueObjects;

namespace LifeManager.Domain.Test.MonthlySummaries
{
    public class TotalIncomeTests
    {
        [Theory]
        [InlineData(-1)]
        [InlineData(-10)]
        [InlineData(-100)]
        public void Create_ShouldThrowDomainException_WhenValueIsNegative(int value)
        {
            const string errorMessageExpected = $"{nameof(TotalIncome)} cannot be negative";
            var exception = Assert.Throws<DomainException>(() => TotalIncome.Create(value));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(55)]
        public void Create_ShouldReturnTotalIncome_WhenValueIsValid(int value)
        {
            var totalIncome = TotalIncome.Create(value);
            Assert.NotNull(totalIncome);
            Assert.IsType<TotalIncome>(totalIncome);
            Assert.Equal(value, totalIncome.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const int value = 1;
            var valueOne = TotalIncome.Create(value);
            var valueTwo = TotalIncome.Create(value);
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const int value = 1;
            var valueOne = TotalIncome.Create(value);
            var valueTwo = TotalIncome.Create(value);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }    
    }
}