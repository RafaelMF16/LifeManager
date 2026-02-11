using LifeManager.Domain.Exceptions;
using LifeManager.Domain.MonthlySummaries.ValueObjects;

namespace LifeManager.Domain.Test.MonthlySummaries
{
    public class MonthlySummaryMonthTests
    {
        [Theory]
        [InlineData(30)]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(13)]
        [InlineData(100)]
        public void Create_ShouldThrowDomainException_WhenValueIsInvalid(int month)
        {
            var errorMessageExpected = $"{nameof(MonthlySummaryMonth)} {month} is invalid month";
            var exception = Assert.Throws<DomainException>(() => MonthlySummaryMonth.Create(month));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(11)]
        [InlineData(12)]
        [InlineData(9)]
        public void Create_ShouldReturnMonthlySummaryMonth_WhenValueIsValid(int month)
        {
            var monthlySummaryMonth = MonthlySummaryMonth.Create(month);
            Assert.NotNull(monthlySummaryMonth);
            Assert.IsType<MonthlySummaryMonth>(monthlySummaryMonth);
            Assert.Equal(month, monthlySummaryMonth.Value);
        }


        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const int value = 11;
            var valueOne = MonthlySummaryMonth.Create(value);
            var valueTwo = MonthlySummaryMonth.Create(value);
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const int value = 11;
            var valueOne = MonthlySummaryMonth.Create(value);
            var valueTwo = MonthlySummaryMonth.Create(value);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}