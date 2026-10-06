using LifeManager.Domain.Shared.ValueObjects;

namespace LifeManager.Domain.Test.Shared
{
    public class YearMonthTests
    {
        [Theory]
        [InlineData("2026-09", 2026, 9)]
        [InlineData("2000-01", 2000, 1)]
        [InlineData("2100-12", 2100, 12)]
        public void TryParse_ShouldReadYearAndMonth_WhenValueIsValid(string value, int expectedYear, int expectedMonth)
        {
            var parsed = YearMonth.TryParse(value, out var yearMonth);

            Assert.True(parsed);
            Assert.Equal(expectedYear, yearMonth.Year);
            Assert.Equal(expectedMonth, yearMonth.Month);
            Assert.Equal(value, yearMonth.ToString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("2026-9")]
        [InlineData("2026/09")]
        [InlineData("2026-00")]
        [InlineData("2026-13")]
        [InlineData("1999-12")]
        [InlineData("2101-01")]
        [InlineData("+026-09")]
        [InlineData("2026-0a")]
        [InlineData("2026-09-01")]
        public void TryParse_ShouldFail_WhenValueIsInvalid(string? value)
        {
            Assert.False(YearMonth.TryParse(value, out _));
        }

        [Theory]
        [InlineData("2026-01", -1, "2025-12")]
        [InlineData("2026-11", 3, "2027-02")]
        [InlineData("2026-09", -12, "2025-09")]
        [InlineData("2026-09", 0, "2026-09")]
        public void AddMonths_ShouldCrossYears(string start, int months, string expected)
        {
            YearMonth.TryParse(start, out var yearMonth);

            Assert.Equal(expected, yearMonth.AddMonths(months).ToString());
        }

        [Theory]
        [InlineData("2024-02", 29)]
        [InlineData("2026-02", 28)]
        [InlineData("2026-04", 30)]
        [InlineData("2026-12", 31)]
        public void LastDay_ShouldBeTheMonthLastDay(string value, int expectedDay)
        {
            YearMonth.TryParse(value, out var yearMonth);

            Assert.Equal(new DateOnly(yearMonth.Year, yearMonth.Month, 1), yearMonth.FirstDay);
            Assert.Equal(new DateOnly(yearMonth.Year, yearMonth.Month, expectedDay), yearMonth.LastDay);
        }

        [Fact]
        public void Equals_ShouldCompareYearAndMonth()
        {
            YearMonth.TryParse("2026-09", out var parsed);

            Assert.Equal(YearMonth.From(new DateOnly(2026, 9, 17)), parsed);
        }
    }
}
