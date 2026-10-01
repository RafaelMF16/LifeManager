using LifeManager.Domain.MonthlySummaries.Errors;
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
        public void Create_ShouldReturnFailure_WhenValueIsInvalid(int month)
        {
            var result = MonthlySummaryMonth.Create(month);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.InvalidMonth, result.Error);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(9)]
        [InlineData(11)]
        [InlineData(12)]
        public void Create_ShouldReturnMonthlySummaryMonth_WhenValueIsValid(int month)
        {
            var result = MonthlySummaryMonth.Create(month);

            Assert.True(result.IsSuccess);
            Assert.Equal(month, result.Value.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const int value = 11;
            var valueOne = MonthlySummaryMonth.Create(value).Value!;
            var valueTwo = MonthlySummaryMonth.Create(value).Value!;

            Assert.True(valueOne.Equals(valueTwo));
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const int value = 11;
            var valueOne = MonthlySummaryMonth.Create(value).Value!;
            var valueTwo = MonthlySummaryMonth.Create(value).Value!;

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
