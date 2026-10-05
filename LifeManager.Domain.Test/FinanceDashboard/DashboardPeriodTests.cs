using LifeManager.Domain.FinanceDashboard.Enums;
using LifeManager.Domain.FinanceDashboard.Errors;
using LifeManager.Domain.FinanceDashboard.ValueObjects;

namespace LifeManager.Domain.Test.FinanceDashboard
{
    public class DashboardPeriodTests
    {
        [Fact]
        public void Create_ShouldSpanBothMonths_WhenValuesAreValid()
        {
            var result = DashboardPeriod.Create("2026-04", "2026-09");

            Assert.True(result.IsSuccess);
            Assert.Equal(6, result.Value.MonthCount);
            Assert.Equal(new DateOnly(2026, 4, 1), result.Value.FirstDay);
            Assert.Equal(new DateOnly(2026, 9, 30), result.Value.LastDay);
            Assert.Equal(["2026-04", "2026-05", "2026-06", "2026-07", "2026-08", "2026-09"], result.Value.Months().Select(month => month.ToString()));
        }

        [Fact]
        public void Create_ShouldAcceptASingleMonth()
        {
            var result = DashboardPeriod.Create("2026-09", "2026-09");

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value.MonthCount);
        }

        [Fact]
        public void Create_ShouldAcceptTheLongestPeriod()
        {
            var result = DashboardPeriod.Create("2024-01", "2026-12");

            Assert.True(result.IsSuccess);
            Assert.Equal(DashboardPeriod.MaxMonths, result.Value.MonthCount);
        }

        [Theory]
        [InlineData("2026-13", "2026-12", "FinanceDashboard.InvalidFrom")]
        [InlineData(null, "2026-12", "FinanceDashboard.InvalidFrom")]
        [InlineData("2026-01", "26-12", "FinanceDashboard.InvalidTo")]
        [InlineData("2026-05", "2026-04", "FinanceDashboard.EndBeforeStart")]
        [InlineData("2023-12", "2026-12", "FinanceDashboard.PeriodTooLong")]
        public void Create_ShouldReturnFailure_WhenPeriodIsInvalid(string? from, string? to, string expectedCode)
        {
            var result = DashboardPeriod.Create(from, to);

            Assert.False(result.IsSuccess);
            Assert.Equal(expectedCode, result.Error.Code);
        }

        [Theory]
        [InlineData(2026, 3, false)]
        [InlineData(2026, 4, true)]
        [InlineData(2026, 9, true)]
        [InlineData(2026, 10, false)]
        [InlineData(2025, 6, false)]
        public void Contains_ShouldIncludeBothEnds(int year, int month, bool expected)
        {
            var period = DashboardPeriod.Create("2026-04", "2026-09").Value!;

            Assert.Equal(expected, period.Contains(year, month));
        }

        [Theory]
        [InlineData("2026-04", "2026-09", "2025-10", "2026-03")]
        [InlineData("2026-01", "2026-01", "2025-12", "2025-12")]
        [InlineData("2025-01", "2025-12", "2024-01", "2024-12")]
        public void ComparisonFor_ShouldTakeTheSameLengthRightBefore_WhenComparisonIsPreviousPeriod(string from, string to, string expectedFrom, string expectedTo)
        {
            var period = DashboardPeriod.Create(from, to).Value!;

            var result = period.ComparisonFor(DashboardComparison.PreviousPeriod);

            Assert.True(result.IsSuccess);
            Assert.Equal(expectedFrom, result.Value.From.ToString());
            Assert.Equal(expectedTo, result.Value.To.ToString());
        }

        [Fact]
        public void ComparisonFor_ShouldTakeTheSameMonthsAYearEarlier_WhenComparisonIsSamePeriodLastYear()
        {
            var period = DashboardPeriod.Create("2026-01", "2026-10").Value!;

            var result = period.ComparisonFor(DashboardComparison.SamePeriodLastYear);

            Assert.True(result.IsSuccess);
            Assert.Equal("2025-01", result.Value.From.ToString());
            Assert.Equal("2025-10", result.Value.To.ToString());
        }

        [Fact]
        public void ComparisonFor_ShouldReturnComparisonTooLong_WhenSamePeriodLastYearWouldOverlap()
        {
            var period = DashboardPeriod.Create("2025-01", "2026-01").Value!;

            var result = period.ComparisonFor(DashboardComparison.SamePeriodLastYear);

            Assert.False(result.IsSuccess);
            Assert.Equal(FinanceDashboardErrors.ComparisonTooLong, result.Error);
        }

        [Fact]
        public void ComparisonFor_ShouldReturnInvalidComparison_WhenComparisonIsNotDefined()
        {
            var period = DashboardPeriod.Create("2026-01", "2026-03").Value!;

            var result = period.ComparisonFor((DashboardComparison)99);

            Assert.False(result.IsSuccess);
            Assert.Equal(FinanceDashboardErrors.InvalidComparison, result.Error);
        }
    }
}
