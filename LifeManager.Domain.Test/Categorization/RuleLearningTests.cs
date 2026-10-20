using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.StatementImports;
using LifeManager.Domain.StatementImports.Enums;

namespace LifeManager.Domain.Test.Categorization
{
    public class RuleLearningTests
    {
        private static readonly DateOnly Today = new(2026, 10, 19);

        private static StatementImportRow Row(
            string description, int? categoryId, bool included = true, MoneyFlowType type = MoneyFlowType.Expense, DateOnly? date = null, bool shiftToNextMonth = false)
        {
            var row = StatementImportRow.Create(new StatementLine(date ?? new DateOnly(2025, 1, 10), description, -10m, null), null, Today).Value!;
            row.Edit(type, categoryId, 10m, "Editada", row.Date, included, Today, shiftToNextMonth);
            return row;
        }

        [Fact]
        public void From_ShouldCountWhetherALineAtTheEndOfItsMonthWasShifted()
        {
            var learnings = RuleLearning.From([
                Row("SALARIO EMPRESA XPTO", 3, type: MoneyFlowType.Income, date: new DateOnly(2025, 8, 29), shiftToNextMonth: true),
                Row("SALARIO EMPRESA XPTO", 3, type: MoneyFlowType.Income, date: new DateOnly(2025, 9, 30), shiftToNextMonth: true),
                Row("FATURA CARTAO", 2, date: new DateOnly(2025, 8, 28))]);

            Assert.Equal(
                [
                    new RuleLearning("fatura cartao", MoneyFlowType.Expense, RuleAction.Categorize, new CategoryId(2), 1),
                    new RuleLearning("fatura cartao", MoneyFlowType.Expense, RuleAction.SameMonth, null, 1),
                    new RuleLearning("salario empresa xpto", MoneyFlowType.Income, RuleAction.Categorize, new CategoryId(3), 2),
                    new RuleLearning("salario empresa xpto", MoneyFlowType.Income, RuleAction.NextMonth, null, 2)
                ],
                learnings);
        }

        [Fact]
        public void From_ShouldLearnNoShiftFromLinesOutsideTheWindowOrLeftOut()
        {
            var learnings = RuleLearning.From([
                Row("SALARIO EMPRESA XPTO", 3, type: MoneyFlowType.Income, date: new DateOnly(2025, 8, 15), shiftToNextMonth: true),
                Row("PIX ENVIADO JOAO", null, included: false, date: new DateOnly(2025, 8, 30))]);

            Assert.DoesNotContain(learnings, learning => learning.Action is RuleAction.NextMonth or RuleAction.SameMonth);
        }

        [Fact]
        public void From_ShouldCountEachIncludedLineAsAUseOfItsCategory()
        {
            var learnings = RuleLearning.From([
                Row("IFOOD *RESTAURANTE A", 1),
                Row("IFOOD *RESTAURANTE B", 1),
                Row("IFOOD *RESTAURANTE C", 2),
                Row("APLICACAO CDB", 4, type: MoneyFlowType.Investment)]);

            Assert.Equal(
                [
                    new RuleLearning("aplicacao cdb", MoneyFlowType.Investment, RuleAction.Categorize, new CategoryId(4), 1),
                    new RuleLearning("ifood restaurante", MoneyFlowType.Expense, RuleAction.Categorize, new CategoryId(1), 2),
                    new RuleLearning("ifood restaurante", MoneyFlowType.Expense, RuleAction.Categorize, new CategoryId(2), 1)
                ],
                learnings);
        }

        [Fact]
        public void From_ShouldLearnToIgnoreLinesLeftOut()
        {
            var learnings = RuleLearning.From([Row("PIX ENVIADO JOAO", null, included: false)]);

            Assert.Equal([new RuleLearning("joao", MoneyFlowType.Expense, RuleAction.Ignore, null, 1)], learnings);
        }

        [Fact]
        public void From_ShouldLearnToIgnoreARedemptionLeftOutByTheDetection()
        {
            var row = StatementImportRow.Create(new StatementLine(new DateOnly(2025, 1, 10), "Resgate RDB", 610m, null), null, Today).Value!;
            row.ApplyInvestmentDetection(InvestmentMovement.Redemption, null);

            var learnings = RuleLearning.From([row]);

            Assert.Equal([new RuleLearning("resgate rdb", MoneyFlowType.Income, RuleAction.Ignore, null, 1)], learnings);
        }

        [Fact]
        public void From_ShouldLearnNothingFromDuplicatesOrUncategorizedLines()
        {
            var duplicate = Row("NETFLIX", null, included: false);
            duplicate.MarkDuplicateOf(1, 1);

            Assert.Empty(RuleLearning.From([duplicate, Row("SPOTIFY", null)]));
        }
    }
}
