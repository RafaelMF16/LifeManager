using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.StatementImports;

namespace LifeManager.Domain.Test.StatementImports
{
    public class ImportRowFilerTests
    {
        private static readonly DateOnly Today = new(2026, 10, 19);
        private static readonly OwnTransferDetector OwnTransfers = OwnTransferDetector.For("Fulano Beltrano Ciclano");

        private const string OwnTransfer = "Pix recebido: \"Cp :90400888-FULANO BELTRANO CICLANO\"";

        private static StatementImportRow Row(string description, decimal amount)
            => StatementImportRow.Create(new StatementLine(new DateOnly(2024, 3, 5), description, amount, null), "account", Today).Value!;

        private static CategorySuggestion Suggestion(SuggestionSource source, MoneyFlowType type, int? categoryId, RuleAction action = RuleAction.Categorize)
            => new(action, type, categoryId is null ? null : new CategoryId(categoryId.Value), source, SuggestionConfidence.Medium, 2);

        [Fact]
        public void File_ShouldFollowTheUsersRule_OverAnInvestmentDetection()
        {
            var row = Row("Aplicação RDB", -500m);

            ImportRowFiler.File(row, Suggestion(SuggestionSource.Rule, MoneyFlowType.Expense, 2), OwnTransfers);

            Assert.Equal(MoneyFlowType.Expense, row.Type);
            Assert.Equal(SuggestionSource.Rule, row.SuggestionSource);
        }

        [Fact]
        public void File_ShouldFollowTheUsersHistory_OverAnOwnTransferDetection()
        {
            var row = Row(OwnTransfer, 1000m);

            ImportRowFiler.File(row, Suggestion(SuggestionSource.History, MoneyFlowType.Income, 3), OwnTransfers);

            Assert.True(row.Included);
            Assert.Equal(new CategoryId(3), row.CategoryId);
            Assert.Equal(SuggestionSource.History, row.SuggestionSource);
        }

        [Fact]
        public void File_ShouldDetectAnInvestment_OverASimilarSuggestion()
        {
            var row = Row("Resgate RDB", 610m);

            ImportRowFiler.File(row, Suggestion(SuggestionSource.Similar, MoneyFlowType.Income, 3), OwnTransfers);

            Assert.False(row.Included);
            Assert.Null(row.CategoryId);
            Assert.Equal(SuggestionSource.InvestmentRedemption, row.SuggestionSource);
        }

        [Fact]
        public void File_ShouldKeepTheCategoryOfASimilarInvestment_WhenAnApplicationIsDetected()
        {
            var row = Row("Aplicação RDB", -500m);

            ImportRowFiler.File(row, Suggestion(SuggestionSource.Similar, MoneyFlowType.Investment, 4), OwnTransfers);

            Assert.Equal(MoneyFlowType.Investment, row.Type);
            Assert.Equal(new CategoryId(4), row.CategoryId);
            Assert.Equal(SuggestionSource.InvestmentApplication, row.SuggestionSource);
        }

        [Fact]
        public void File_ShouldNotKeepTheCategoryOfASimilarExpense_WhenAnApplicationIsDetected()
        {
            var row = Row("Aplicação RDB", -500m);

            ImportRowFiler.File(row, Suggestion(SuggestionSource.Similar, MoneyFlowType.Expense, 2), OwnTransfers);

            Assert.Equal(MoneyFlowType.Investment, row.Type);
            Assert.Null(row.CategoryId);
        }

        [Fact]
        public void File_ShouldDetectAnOwnTransfer_OverASimilarSuggestion()
        {
            var row = Row(OwnTransfer, 1000m);

            ImportRowFiler.File(row, Suggestion(SuggestionSource.Similar, MoneyFlowType.Income, 3), OwnTransfers);

            Assert.False(row.Included);
            Assert.Null(row.CategoryId);
            Assert.Equal(SuggestionSource.OwnTransfer, row.SuggestionSource);
        }

        [Fact]
        public void File_ShouldApplyASimilarSuggestion_WhenNothingIsDetected()
        {
            var row = Row("COMPRA CARTAO IFOOD *RESTAURANTE", -42.9m);

            ImportRowFiler.File(row, Suggestion(SuggestionSource.Similar, MoneyFlowType.Expense, 1), OwnTransfers);

            Assert.Equal(new CategoryId(1), row.CategoryId);
            Assert.Equal(SuggestionSource.Similar, row.SuggestionSource);
        }

        [Fact]
        public void File_ShouldLeaveTheRowAsTheBankSignSaysIt_WhenThereIsNothingToGoBy()
        {
            var row = Row("COMPRA CARTAO IFOOD *RESTAURANTE", -42.9m);

            ImportRowFiler.File(row, null, OwnTransfers);

            Assert.Equal(MoneyFlowType.Expense, row.Type);
            Assert.True(row.Included);
            Assert.Equal(SuggestionSource.None, row.SuggestionSource);
        }
    }
}
