using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.Errors;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.MonthlySummaries
{
    public class MonthlySummaryTests
    {
        private const int CurrentYear = 2026;

        [Fact]
        public void Create_ShouldReturnMonthlySummaryWithZeroTotals_WhenValuesAreValid()
        {
            const int userId = 1;
            const int month = 11;

            var result = MonthlySummary.Create(userId, month, CurrentYear, CurrentYear);

            Assert.True(result.IsSuccess);
            var monthlySummary = result.Value;
            Assert.Null(monthlySummary.Id);
            Assert.Equal(userId, monthlySummary.UserId.Value);
            Assert.Equal(month, monthlySummary.Month.Value);
            Assert.Equal(CurrentYear, monthlySummary.Year.Value);
            Assert.Equal(0m, monthlySummary.TotalIncome.Value);
            Assert.Equal(0m, monthlySummary.TotalExpense.Value);
            Assert.Equal(0m, monthlySummary.TotalInvestment.Value);
            Assert.Equal(0m, monthlySummary.Balance.Value);
            Assert.Equal(monthlySummary.Balance.Value, monthlySummary.BalanceAmount);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenMonthIsInvalid()
        {
            var result = MonthlySummary.Create(1, 13, CurrentYear, CurrentYear);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.InvalidMonth, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenYearIsNeitherCurrentNorPrevious()
        {
            var result = MonthlySummary.Create(1, 5, CurrentYear - 2, CurrentYear);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.YearNotCurrent, result.Error);
        }

        [Fact]
        public void Create_ShouldOpenPreviousYearsMonth_WhenYearIsPrevious()
        {
            var result = MonthlySummary.Create(1, 12, CurrentYear - 1, CurrentYear);

            Assert.True(result.IsSuccess);
            Assert.Equal(CurrentYear - 1, result.Value!.Year.Value);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenYearIsNext()
        {
            var result = MonthlySummary.Create(1, 5, CurrentYear + 1, CurrentYear);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.YearNotCurrent, result.Error);
        }

        [Fact]
        public void AssignId_ShouldSetId_WhenMonthlySummaryHasNoIdYet()
        {
            var monthlySummary = MonthlySummary.Create(1, 11, CurrentYear, CurrentYear).Value!;

            monthlySummary.AssignId(10);

            Assert.NotNull(monthlySummary.Id);
            Assert.Equal(10, monthlySummary.Id!.Value);
        }

        [Theory]
        [InlineData(1000, 250.5, 0, 749.5)]
        [InlineData(100, 300, 0, -200)]
        [InlineData(0, 0, 0, 0)]
        [InlineData(1000, 300, 200, 500)]
        [InlineData(100, 0, 300, -200)]
        public void ApplyTotals_ShouldReplaceTotalsAndKeepBalanceInSync(decimal totalIncome, decimal totalExpense, decimal totalInvestment, decimal expectedBalance)
        {
            var monthlySummary = MonthlySummary.Create(1, 11, CurrentYear, CurrentYear).Value!;

            var result = monthlySummary.ApplyTotals(totalIncome, totalExpense, totalInvestment);

            Assert.True(result.IsSuccess);
            Assert.Equal(totalIncome, monthlySummary.TotalIncome.Value);
            Assert.Equal(totalExpense, monthlySummary.TotalExpense.Value);
            Assert.Equal(totalInvestment, monthlySummary.TotalInvestment.Value);
            Assert.Equal(expectedBalance, monthlySummary.Balance.Value);
            Assert.Equal(expectedBalance, monthlySummary.BalanceAmount);
        }

        [Fact]
        public void ApplyTotals_ShouldKeepCurrentTotals_WhenATotalIsNegative()
        {
            var monthlySummary = MonthlySummary.Create(1, 11, CurrentYear, CurrentYear).Value!;

            var result = monthlySummary.ApplyTotals(-1, 10, 0);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.TotalIncomeNegative, result.Error);
            Assert.Equal(0, monthlySummary.TotalExpense.Value);
            Assert.Equal(0, monthlySummary.BalanceAmount);
        }

        [Fact]
        public void ApplyTotals_ShouldKeepCurrentTotals_WhenInvestmentIsNegative()
        {
            var monthlySummary = MonthlySummary.Create(1, 11, CurrentYear, CurrentYear).Value!;

            var result = monthlySummary.ApplyTotals(100, 10, -1);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.TotalInvestmentNegative, result.Error);
            Assert.Equal(0, monthlySummary.TotalIncome.Value);
            Assert.Equal(0, monthlySummary.TotalExpense.Value);
            Assert.Equal(0, monthlySummary.BalanceAmount);
        }

        [Fact]
        public void Equals_ShouldCompareById()
        {
            var first = MonthlySummary.Create(1, 1, CurrentYear, CurrentYear).Value!;
            var second = MonthlySummary.Create(1, 2, CurrentYear, CurrentYear).Value!;
            first.AssignId(7);
            second.AssignId(7);

            Assert.True(first.Equals(second));
        }

        [Fact]
        public void Equals_ShouldBeFalse_WhenIdsAreMissing()
        {
            var first = MonthlySummary.Create(1, 1, CurrentYear, CurrentYear).Value!;
            var second = MonthlySummary.Create(1, 1, CurrentYear, CurrentYear).Value!;

            Assert.False(first.Equals(second));
        }

        [Fact]
        public void OpenForRecurringPosting_ShouldOpenAnEmptyMonth_EvenInAnotherYear()
        {
            YearMonth.TryParse($"{CurrentYear - 1}-12", out var lastDecember);

            var monthlySummary = MonthlySummary.OpenForRecurringPosting(new UserId(3), lastDecember);

            Assert.Null(monthlySummary.Id);
            Assert.Equal(3, monthlySummary.UserId.Value);
            Assert.Equal(12, monthlySummary.Month.Value);
            Assert.Equal(CurrentYear - 1, monthlySummary.Year.Value);
            Assert.Equal(0m, monthlySummary.TotalIncome.Value);
            Assert.Equal(0m, monthlySummary.TotalExpense.Value);
            Assert.Equal(0m, monthlySummary.TotalInvestment.Value);
            Assert.Equal(0m, monthlySummary.BalanceAmount);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void OpenForEntry_ShouldOpenAnEmptyMonth_WhenYearIsCurrentOrPrevious(int offsetFromCurrentYear)
        {
            var month = YearMonth.From(new DateOnly(CurrentYear + offsetFromCurrentYear, 12, 1));

            var result = MonthlySummary.OpenForEntry(new UserId(3), month, CurrentYear);

            Assert.True(result.IsSuccess);
            var monthlySummary = result.Value!;
            Assert.Null(monthlySummary.Id);
            Assert.Equal(3, monthlySummary.UserId.Value);
            Assert.Equal(12, monthlySummary.Month.Value);
            Assert.Equal(CurrentYear + offsetFromCurrentYear, monthlySummary.Year.Value);
            Assert.Equal(0m, monthlySummary.BalanceAmount);
        }

        [Theory]
        [InlineData(-2)]
        [InlineData(1)]
        public void OpenForEntry_ShouldReturnYearNotCurrent_WhenYearIsNeitherCurrentNorPrevious(int offsetFromCurrentYear)
        {
            var result = MonthlySummary.OpenForEntry(new UserId(3), YearMonth.From(new DateOnly(CurrentYear + offsetFromCurrentYear, 5, 1)), CurrentYear);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.YearNotCurrent, result.Error);
        }
    }
}
