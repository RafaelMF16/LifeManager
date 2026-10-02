using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.Errors;

namespace LifeManager.Domain.Test.MonthlySummaries
{
    public class MonthlySummaryTests
    {
        private static readonly int CurrentYear = DateTimeOffset.UtcNow.Year;

        [Fact]
        public void Create_ShouldReturnMonthlySummaryWithZeroTotals_WhenValuesAreValid()
        {
            const int userId = 1;
            const int month = 11;

            var result = MonthlySummary.Create(userId, month, CurrentYear);

            Assert.True(result.IsSuccess);
            var monthlySummary = result.Value;
            Assert.Null(monthlySummary.Id);
            Assert.Equal(userId, monthlySummary.UserId.Value);
            Assert.Equal(month, monthlySummary.Month.Value);
            Assert.Equal(CurrentYear, monthlySummary.Year.Value);
            Assert.Equal(0m, monthlySummary.TotalIncome.Value);
            Assert.Equal(0m, monthlySummary.TotalExpense.Value);
            Assert.Equal(0m, monthlySummary.Balance.Value);
            Assert.Equal(monthlySummary.Balance.Value, monthlySummary.BalanceAmount);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenMonthIsInvalid()
        {
            var result = MonthlySummary.Create(1, 13, CurrentYear);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.InvalidMonth, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenYearIsNotCurrent()
        {
            var result = MonthlySummary.Create(1, 5, CurrentYear - 1);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.YearNotCurrent, result.Error);
        }

        [Fact]
        public void AssignId_ShouldSetId_WhenMonthlySummaryHasNoIdYet()
        {
            var monthlySummary = MonthlySummary.Create(1, 11, CurrentYear).Value!;

            monthlySummary.AssignId(10);

            Assert.NotNull(monthlySummary.Id);
            Assert.Equal(10, monthlySummary.Id!.Value);
        }

        [Theory]
        [InlineData(1000, 250.5, 749.5)]
        [InlineData(100, 300, -200)]
        [InlineData(0, 0, 0)]
        public void ApplyTotals_ShouldReplaceTotalsAndKeepBalanceInSync(decimal totalIncome, decimal totalExpense, decimal expectedBalance)
        {
            var monthlySummary = MonthlySummary.Create(1, 11, CurrentYear).Value!;

            var result = monthlySummary.ApplyTotals(totalIncome, totalExpense);

            Assert.True(result.IsSuccess);
            Assert.Equal(totalIncome, monthlySummary.TotalIncome.Value);
            Assert.Equal(totalExpense, monthlySummary.TotalExpense.Value);
            Assert.Equal(expectedBalance, monthlySummary.Balance.Value);
            Assert.Equal(expectedBalance, monthlySummary.BalanceAmount);
        }

        [Fact]
        public void ApplyTotals_ShouldKeepCurrentTotals_WhenATotalIsNegative()
        {
            var monthlySummary = MonthlySummary.Create(1, 11, CurrentYear).Value!;

            var result = monthlySummary.ApplyTotals(-1, 10);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.TotalIncomeNegative, result.Error);
            Assert.Equal(0, monthlySummary.TotalExpense.Value);
            Assert.Equal(0, monthlySummary.BalanceAmount);
        }

        [Fact]
        public void Equals_ShouldCompareById()
        {
            var first = MonthlySummary.Create(1, 1, CurrentYear).Value!;
            var second = MonthlySummary.Create(1, 2, CurrentYear).Value!;
            first.AssignId(7);
            second.AssignId(7);

            Assert.True(first.Equals(second));
        }

        [Fact]
        public void Equals_ShouldBeFalse_WhenIdsAreMissing()
        {
            var first = MonthlySummary.Create(1, 1, CurrentYear).Value!;
            var second = MonthlySummary.Create(1, 1, CurrentYear).Value!;

            Assert.False(first.Equals(second));
        }
    }
}
