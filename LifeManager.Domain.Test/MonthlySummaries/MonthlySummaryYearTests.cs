using LifeManager.Domain.MonthlySummaries.Errors;
using LifeManager.Domain.MonthlySummaries.ValueObjects;

namespace LifeManager.Domain.Test.MonthlySummaries
{
    public class MonthlySummaryYearTests
    {
        private const int CurrentYear = 2026;

        [Theory]
        [InlineData(-2)]
        [InlineData(-1000)]
        [InlineData(1)]
        [InlineData(1000)]
        public void Create_ShouldReturnFailure_WhenYearIsNeitherCurrentNorPrevious(int offsetFromCurrentYear)
        {
            var result = MonthlySummaryYear.Create(CurrentYear + offsetFromCurrentYear, CurrentYear);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.YearNotCurrent, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnMonthlySummaryYear_WhenValueIsCurrentYear()
        {
            var result = MonthlySummaryYear.Create(CurrentYear, CurrentYear);

            Assert.True(result.IsSuccess);
            Assert.Equal(CurrentYear, result.Value!.Value);
        }

        [Fact]
        public void Create_ShouldReturnMonthlySummaryYear_WhenValueIsPreviousYear()
        {
            var result = MonthlySummaryYear.Create(CurrentYear - 1, CurrentYear);

            Assert.True(result.IsSuccess);
            Assert.Equal(CurrentYear - 1, result.Value!.Value);
        }

        [Fact]
        public void Create_ShouldCompareWithTheGivenCurrentYear_NotWithTheClock()
        {
            var result = MonthlySummaryYear.Create(1999, 1999);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            var valueOne = MonthlySummaryYear.Create(CurrentYear, CurrentYear).Value!;
            var valueTwo = MonthlySummaryYear.Create(CurrentYear, CurrentYear).Value!;

            Assert.True(valueOne.Equals(valueTwo));
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            var valueOne = MonthlySummaryYear.Create(CurrentYear, CurrentYear).Value!;
            var valueTwo = MonthlySummaryYear.Create(CurrentYear, CurrentYear).Value!;

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
