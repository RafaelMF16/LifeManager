using LifeManager.Domain.MonthlySummaries.Errors;
using LifeManager.Domain.MonthlySummaries.ValueObjects;

namespace LifeManager.Domain.Test.MonthlySummaries
{
    public class MonthlySummaryYearTests
    {
        [Theory]
        [InlineData(-1)]
        [InlineData(1)]
        [InlineData(1000)]
        public void Create_ShouldReturnFailure_WhenYearIsNotCurrent(int offsetFromCurrentYear)
        {
            var result = MonthlySummaryYear.Create(DateTimeOffset.UtcNow.Year + offsetFromCurrentYear);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.YearNotCurrent, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnMonthlySummaryYear_WhenValueIsCurrentYear()
        {
            var value = DateTimeOffset.UtcNow.Year;

            var result = MonthlySummaryYear.Create(value);

            Assert.True(result.IsSuccess);
            Assert.Equal(value, result.Value.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            var value = DateTimeOffset.UtcNow.Year;
            var valueOne = MonthlySummaryYear.Create(value).Value!;
            var valueTwo = MonthlySummaryYear.Create(value).Value!;

            Assert.True(valueOne.Equals(valueTwo));
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            var value = DateTimeOffset.UtcNow.Year;
            var valueOne = MonthlySummaryYear.Create(value).Value!;
            var valueTwo = MonthlySummaryYear.Create(value).Value!;

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
