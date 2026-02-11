using LifeManager.Domain.Exceptions;
using LifeManager.Domain.MonthlySummaries.ValueObjects;

namespace LifeManager.Domain.Test.MonthlySummaries
{
    public class MonthlySummaryYearTests
    {
        [Theory]
        [InlineData(2025)]
        [InlineData(2024)]
        [InlineData(2027)]
        [InlineData(3000)]
        public void Create_ShouldThrowDomainException_WhenYearIsInvalid(int year)
        {
            const string errorMessageExpected = $"{nameof(MonthlySummaryYear)} can only be created for the current year";
            var exception = Assert.Throws<DomainException>(() => MonthlySummaryYear.Create(year));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldReturnMonthlySummaryYear_WhenValueIsValid()
        {
            var value = DateTimeOffset.UtcNow.Year;
            var monthlySummaryYear = MonthlySummaryYear.Create(value);

            Assert.NotNull(monthlySummaryYear);
            Assert.IsType<MonthlySummaryYear>(monthlySummaryYear);
            Assert.Equal(value, monthlySummaryYear.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            var value = DateTimeOffset.UtcNow.Year;
            var valueOne = MonthlySummaryYear.Create(value);
            var valueTwo = MonthlySummaryYear.Create(value);
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            var value = DateTimeOffset.UtcNow.Year;
            var valueOne = MonthlySummaryYear.Create(value);
            var valueTwo = MonthlySummaryYear.Create(value);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}