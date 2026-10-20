using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.StatementImports;

namespace LifeManager.Domain.Test.StatementImports
{
    public class MonthShiftAdvisorTests
    {
        private static readonly DateOnly Today = new(2026, 10, 19);

        private static CategorizationRule Rule(RuleAction action, int hits, MoneyFlowType type = MoneyFlowType.Income, DateOnly? lastUsedOn = null)
            => CategorizationRule.FromPersistence(1, 1, "salario empresa xpto", type, action, null, hits, lastUsedOn ?? Today);

        private static StatementImportRow Row(DateOnly date, decimal amount = 5000m, string description = "SALARIO EMPRESA XPTO")
            => StatementImportRow.Create(new StatementLine(date, description, amount, null), null, Today).Value!;

        private static bool ShouldShift(StatementImportRow row, params CategorizationRule[] rules)
            => new MonthShiftAdvisor(rules, Today).ShouldShift(row);

        [Fact]
        public void ShouldShift_ShouldBeTrue_WhenTheMerchantWentToTheNextMonthAndTheLineIsAtTheEndOfItsMonth()
        {
            Assert.True(ShouldShift(Row(new DateOnly(2026, 9, 30)), Rule(RuleAction.NextMonth, 1)));
            Assert.True(ShouldShift(Row(new DateOnly(2026, 9, 26)), Rule(RuleAction.NextMonth, 1)));
        }

        [Fact]
        public void ShouldShift_ShouldBeFalse_OutsideTheLastDaysOfTheMonth()
        {
            Assert.False(ShouldShift(Row(new DateOnly(2026, 9, 15)), Rule(RuleAction.NextMonth, 3)));
            Assert.False(ShouldShift(Row(new DateOnly(2026, 9, 25)), Rule(RuleAction.NextMonth, 3)));
        }

        [Fact]
        public void ShouldShift_ShouldBeFalse_WithoutRulesAboutTheMonth()
        {
            Assert.False(ShouldShift(Row(new DateOnly(2026, 9, 30))));
            Assert.False(ShouldShift(
                Row(new DateOnly(2026, 9, 30)),
                CategorizationRule.FromPersistence(1, 1, "salario empresa xpto", MoneyFlowType.Income, RuleAction.Categorize, 3, 5, Today)));
        }

        [Fact]
        public void ShouldShift_ShouldFollowTheHeavierDecision()
        {
            Assert.False(ShouldShift(Row(new DateOnly(2026, 9, 30)), Rule(RuleAction.NextMonth, 1), Rule(RuleAction.SameMonth, 1)));
            Assert.False(ShouldShift(Row(new DateOnly(2026, 9, 30)), Rule(RuleAction.NextMonth, 1), Rule(RuleAction.SameMonth, 2)));
            Assert.True(ShouldShift(Row(new DateOnly(2026, 9, 30)), Rule(RuleAction.NextMonth, 2), Rule(RuleAction.SameMonth, 1)));
        }

        [Fact]
        public void ShouldShift_ShouldWeighRecentDecisionsMore()
        {
            var row = Row(new DateOnly(2026, 9, 30));

            Assert.True(ShouldShift(row, Rule(RuleAction.NextMonth, 2), Rule(RuleAction.SameMonth, 3, lastUsedOn: Today.AddYears(-2))));
        }

        [Fact]
        public void ShouldShift_ShouldOnlyCountDecisionsOfTheSameType()
        {
            var expense = Row(new DateOnly(2026, 9, 30), amount: -5000m);

            Assert.False(ShouldShift(expense, Rule(RuleAction.NextMonth, 3, MoneyFlowType.Income)));
            Assert.True(ShouldShift(expense, Rule(RuleAction.NextMonth, 1, MoneyFlowType.Expense)));
        }

        [Fact]
        public void ShouldShift_ShouldBeFalse_ForALineLeftOut()
        {
            var row = Row(new DateOnly(2026, 9, 30));
            row.ApplyOwnTransferDetection();

            Assert.False(ShouldShift(row, Rule(RuleAction.NextMonth, 3)));
        }
    }
}
