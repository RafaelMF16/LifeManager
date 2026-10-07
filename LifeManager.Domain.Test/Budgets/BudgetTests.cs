using LifeManager.Domain.Budgets;
using LifeManager.Domain.Budgets.Errors;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;

namespace LifeManager.Domain.Test.Budgets
{
    public class BudgetTests
    {
        private static YearMonth Month(string value)
        {
            YearMonth.TryParse(value, out var month);
            return month;
        }

        [Fact]
        public void Create_ShouldReturnOpenEndedVersion_WhenTheValuesAreValid()
        {
            var result = Budget.Create(1, MoneyFlowType.Expense, 3, 1200m, Month("2026-10"));

            Assert.True(result.IsSuccess);
            var budget = result.Value;
            Assert.Null(budget.Id);
            Assert.Equal(1, budget.UserId.Value);
            Assert.Equal(3, budget.CategoryId!.Value);
            Assert.Equal(1200m, budget.Amount.Value);
            Assert.Equal(new DateOnly(2026, 10, 1), budget.EffectiveFrom);
            Assert.Null(budget.EffectiveTo);
            Assert.Equal(Month("2026-10"), budget.FromMonth);
            Assert.Null(budget.ToMonth);
        }

        [Fact]
        public void Create_ShouldAllowAGoalOnTheMonthTotal()
        {
            var budget = Budget.Create(1, MoneyFlowType.Investment, null, 2000m, Month("2026-10")).Value!;

            Assert.Null(budget.CategoryId);
        }

        [Theory]
        [InlineData(MoneyFlowType.Income)]
        [InlineData((MoneyFlowType)99)]
        public void Create_ShouldReturnInvalidType_WhenNotExpenseOrInvestment(MoneyFlowType type)
        {
            Assert.Equal(BudgetErrors.InvalidType, Budget.Create(1, type, null, 100m, Month("2026-10")).Error);
        }

        [Theory]
        [InlineData(0, "Budget.AmountNotPositive")]
        [InlineData(-5, "Budget.AmountNotPositive")]
        [InlineData(10.123, "Budget.AmountTooManyDecimals")]
        [InlineData(1_000_000_000_000, "Budget.AmountTooLarge")]
        public void Create_ShouldReturnAmountError_WhenAmountIsInvalid(decimal amount, string expectedCode)
        {
            Assert.Equal(expectedCode, Budget.Create(1, MoneyFlowType.Expense, null, amount, Month("2026-10")).Error!.Code);
        }

        [Fact]
        public void Covers_ShouldIncludeBothEndMonths()
        {
            var budget = Budget.FromPersistence(1, 1, MoneyFlowType.Expense, 3, 100m, new DateOnly(2026, 3, 1), new DateOnly(2026, 5, 1));

            Assert.False(budget.Covers(Month("2026-02")));
            Assert.True(budget.Covers(Month("2026-03")));
            Assert.True(budget.Covers(Month("2026-05")));
            Assert.False(budget.Covers(Month("2026-06")));
        }

        [Fact]
        public void Covers_ShouldIncludeEveryLaterMonth_WhenOpenEnded()
        {
            var budget = Budget.Create(1, MoneyFlowType.Expense, 3, 100m, Month("2026-03")).Value!;

            Assert.True(budget.Covers(Month("2040-01")));
        }

        [Theory]
        [InlineData(MoneyFlowType.Expense, 800, true, 0)]
        [InlineData(MoneyFlowType.Expense, 1000, true, 0)]
        [InlineData(MoneyFlowType.Expense, 1250, false, 250)]
        [InlineData(MoneyFlowType.Investment, 1250, true, 0)]
        [InlineData(MoneyFlowType.Investment, 1000, true, 0)]
        [InlineData(MoneyFlowType.Investment, 600, false, 400)]
        public void IsMetByAndGapFor_ShouldTreatExpensesAsLimitsAndInvestmentsAsTargets(MoneyFlowType type, decimal actual, bool expectedMet, decimal expectedGap)
        {
            var budget = Budget.Create(1, type, null, 1000m, Month("2026-10")).Value!;

            Assert.Equal(expectedMet, budget.IsMetBy(actual));
            Assert.Equal(expectedGap, budget.GapFor(actual));
        }

        [Fact]
        public void ProgressOf_ShouldBeActualOverGoal()
        {
            var budget = Budget.Create(1, MoneyFlowType.Expense, null, 300m, Month("2026-10")).Value!;

            Assert.Equal(0.3333m, budget.ProgressOf(100m));
            Assert.Equal(1.5m, budget.ProgressOf(450m));
        }
    }
}
