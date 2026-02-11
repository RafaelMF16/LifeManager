using LifeManager.Domain.Exceptions;
using LifeManager.Domain.MonthlySummaries.ValueObjects;

namespace LifeManager.Domain.Test.MonthlySummaries
{
    public class TotalExpenseTests
    {
        [Theory]
        [InlineData(-1)]
        [InlineData(-10)]
        [InlineData(-100)]
        public void Create_ShouldThrowDomainException_WhenValueIsNegative(int value)
        {
            const string errorMessageExpected = $"{nameof(TotalExpense)} cannot be negative";
            var exception = Assert.Throws<DomainException>(() => TotalExpense.Create(value));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(55)]
        public void Create_ShouldReturnTotalExpense_WhenValueIsValid(int value)
        {
            var totalExpense = TotalExpense.Create(value);
            Assert.NotNull(totalExpense);
            Assert.IsType<TotalExpense>(totalExpense);
            Assert.Equal(value, totalExpense.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const int value = 1;
            var valueOne = TotalExpense.Create(value);
            var valueTwo = TotalExpense.Create(value);
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const int value = 1;
            var valueOne = TotalExpense.Create(value);
            var valueTwo = TotalExpense.Create(value);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}