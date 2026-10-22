using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.StatementImports;
using LifeManager.Domain.StatementImports.Enums;
using LifeManager.Domain.StatementImports.Errors;
using LifeManager.Domain.Transactions.Errors;

namespace LifeManager.Domain.Test.StatementImports
{
    public class StatementImportRowTests
    {
        private static readonly DateOnly Today = new(2026, 10, 19);

        private static StatementImportRow Row(decimal amount = -42.9m, string description = "COMPRA CARTAO IFOOD *RESTAURANTE", DateOnly? date = null, string? fitid = null)
            => StatementImportRow.Create(new StatementLine(date ?? new DateOnly(2024, 5, 3), description, amount, fitid), "account", Today).Value!;

        [Fact]
        public void Create_ShouldReadAnOutgoingLineAsAnIncludedExpense()
        {
            var row = Row();

            Assert.Equal(MoneyFlowType.Expense, row.Type);
            Assert.Equal(42.9m, row.Amount.Value);
            Assert.Equal(-42.9m, row.SignedAmount);
            Assert.Equal("COMPRA CARTAO IFOOD *RESTAURANTE", row.Description.Value);
            Assert.Equal("ifood restaurante", row.MerchantKey);
            Assert.True(row.Included);
            Assert.Null(row.CategoryId);
            Assert.Equal(SuggestionSource.None, row.SuggestionSource);
            Assert.False(row.EditedByUser);
        }

        [Fact]
        public void Create_ShouldReadAnIncomingLineAsIncome()
        {
            var row = Row(amount: 5000m, description: "SALARIO EMPRESA XPTO");

            Assert.Equal(MoneyFlowType.Income, row.Type);
            Assert.Equal(5000m, row.SignedAmount);
        }

        [Fact]
        public void Create_ShouldCollapseSpacesAndCutTheDescriptionToWhatATransactionHolds()
        {
            var longText = "PAGAMENTO   " + new string('X', 300);

            var row = Row(description: longText);

            Assert.Equal(StatementImportRow.OriginalDescriptionMaxLength, row.OriginalDescription.Length);
            Assert.StartsWith("PAGAMENTO X", row.OriginalDescription);
            Assert.Equal(80, row.Description.Value.Length);
        }

        [Fact]
        public void Create_ShouldIdentifyTheBankLine_WhenTheFileHasAnAccountAndALineId()
        {
            Assert.Equal(ImportExternalId.From("account", "FIT-1"), Row(fitid: "FIT-1").ExternalId);
            Assert.Null(Row(fitid: null).ExternalId);
        }

        [Fact]
        public void Create_ShouldFail_WhenTheAmountIsZero()
        {
            var result = StatementImportRow.Create(new StatementLine(Today, "Tarifa", 0m, null), null, Today);

            Assert.Equal(StatementImportErrors.AmountZero, result.Error);
        }

        [Fact]
        public void Create_ShouldFail_WhenTheAmountHasMoreThanTwoDecimals()
        {
            var result = StatementImportRow.Create(new StatementLine(Today, "Tarifa", -1.234m, null), null, Today);

            Assert.Equal(TransactionErrors.AmountTooManyDecimals, result.Error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public void Create_ShouldFail_WhenTheDescriptionIsBlank(string? description)
        {
            var result = StatementImportRow.Create(new StatementLine(Today, description, -1m, null), null, Today);

            Assert.Equal(TransactionErrors.DescriptionIsNullOrWhiteSpace, result.Error);
        }

        [Theory]
        [InlineData(1999, 12, 31)]
        [InlineData(2026, 10, 20)]
        public void Create_ShouldFail_WhenTheDateIsBefore2000OrInTheFuture(int year, int month, int day)
        {
            var result = StatementImportRow.Create(new StatementLine(new DateOnly(year, month, day), "Tarifa", -1m, null), null, Today);

            Assert.Equal(StatementImportErrors.DateOutOfRange, result.Error);
        }

        [Fact]
        public void Create_ShouldAcceptToday()
        {
            Assert.True(StatementImportRow.Create(new StatementLine(Today, "Tarifa", -1m, null), null, Today).IsSuccess);
        }

        [Fact]
        public void ApplySuggestion_ShouldFileTheRow()
        {
            var row = Row();

            row.ApplySuggestion(new CategorySuggestion(RuleAction.Categorize, MoneyFlowType.Investment, new CategoryId(4), SuggestionSource.Rule, SuggestionConfidence.High, 3));

            Assert.Equal(MoneyFlowType.Investment, row.Type);
            Assert.Equal(-42.9m, row.SignedAmount);
            Assert.Equal(new CategoryId(4), row.CategoryId);
            Assert.Equal(SuggestionSource.Rule, row.SuggestionSource);
            Assert.Equal(SuggestionConfidence.High, row.SuggestionConfidence);
            Assert.Equal(3, row.SuggestionUses);
            Assert.True(row.Included);
        }

        [Fact]
        public void ApplySuggestion_ShouldLeaveTheRowOut_WhenTheSuggestionIsToIgnore()
        {
            var row = Row();

            row.ApplySuggestion(new CategorySuggestion(RuleAction.Ignore, MoneyFlowType.Expense, null, SuggestionSource.Rule, SuggestionConfidence.High, 2));

            Assert.False(row.Included);
            Assert.Null(row.CategoryId);
        }

        [Fact]
        public void MarkDuplicateOf_ShouldLeaveTheRowOut()
        {
            var row = Row();

            row.MarkDuplicateOf(10, 3);

            Assert.True(row.IsDuplicate);
            Assert.Equal(10, row.DuplicateOfTransactionId);
            Assert.Equal(3, row.DuplicateOfMonthlySummaryId);
            Assert.False(row.Included);
        }

        [Fact]
        public void Edit_ShouldChangeEveryFieldAndMarkTheRowAsEdited()
        {
            var row = Row();

            var result = row.Edit(MoneyFlowType.Income, 7, 10.5m, "  Reembolso  ", new DateOnly(2023, 1, 31), included: false, Today);

            Assert.True(result.IsSuccess);
            Assert.Equal(MoneyFlowType.Income, row.Type);
            Assert.Equal(new CategoryId(7), row.CategoryId);
            Assert.Equal(10.5m, row.SignedAmount);
            Assert.Equal("Reembolso", row.Description.Value);
            Assert.Equal("reembolso", row.NormalizedDescription);
            Assert.Equal(new DateOnly(2023, 1, 31), row.Date);
            Assert.False(row.Included);
            Assert.True(row.EditedByUser);
            Assert.Equal("ifood restaurante", row.MerchantKey);
        }

        [Fact]
        public void Edit_ShouldFail_WhenTheDateIsInTheFuture()
        {
            var row = Row();

            var result = row.Edit(MoneyFlowType.Expense, null, 10m, "Mercado", Today.AddDays(1), included: true, Today);

            Assert.Equal(StatementImportErrors.DateOutOfRange, result.Error);
            Assert.False(row.EditedByUser);
        }

        [Fact]
        public void Edit_ShouldFail_WhenTheTypeIsNotDefined()
        {
            var result = Row().Edit((MoneyFlowType)9, null, 10m, "Mercado", Today, included: true, Today);

            Assert.Equal(TransactionErrors.InvalidType, result.Error);
        }

        [Fact]
        public void EditInBulk_ShouldOnlyChangeTheGivenValues()
        {
            var row = Row();
            row.Edit(MoneyFlowType.Expense, 2, 42.9m, "Almoço", row.Date, included: true, Today);

            row.EditInBulk(type: null, idCategory: 5, included: null);

            Assert.Equal(new CategoryId(5), row.CategoryId);
            Assert.Equal(MoneyFlowType.Expense, row.Type);
            Assert.True(row.Included);
            Assert.True(row.EditedByUser);
        }

        [Fact]
        public void CopyDecisionFrom_ShouldFileTheRowLikeTheSourceAndDropItsSuggestion()
        {
            var source = Row();
            source.Edit(MoneyFlowType.Investment, 4, 42.9m, "Aplicação", source.Date, included: true, Today);
            var row = Row();
            row.ApplySuggestion(new CategorySuggestion(RuleAction.Categorize, MoneyFlowType.Expense, new CategoryId(1), SuggestionSource.Similar, SuggestionConfidence.Low, 1));

            row.CopyDecisionFrom(source);

            Assert.Equal(MoneyFlowType.Investment, row.Type);
            Assert.Equal(new CategoryId(4), row.CategoryId);
            Assert.True(row.Included);
            Assert.Equal(SuggestionSource.None, row.SuggestionSource);
            Assert.Null(row.SuggestionConfidence);
            Assert.False(row.EditedByUser);
        }

        [Fact]
        public void CopyDecisionFrom_ShouldKeepADuplicateOut()
        {
            var source = Row();
            source.Edit(MoneyFlowType.Expense, 1, 42.9m, "Almoço", source.Date, included: true, Today);
            var duplicate = Row();
            duplicate.MarkDuplicateOf(10, 3);

            duplicate.CopyDecisionFrom(source);

            Assert.False(duplicate.Included);
            Assert.Equal(new CategoryId(1), duplicate.CategoryId);
        }

        [Fact]
        public void Create_ShouldKeepTheRowInTheMonthOfItsDate()
        {
            var row = Row(date: new DateOnly(2026, 8, 30));

            Assert.False(row.ShiftToNextMonth);
            Assert.False(row.ShiftLearned);
            Assert.Equal(new DateOnly(2026, 8, 30), row.EffectiveDate);
            Assert.Equal("2026-08", row.Month.ToString());
        }

        [Fact]
        public void Edit_ShouldMoveTheRowToTheFirstDayOfTheNextMonth_WhenShifted()
        {
            var row = Row(amount: 5000m, description: "SALARIO EMPRESA XPTO", date: new DateOnly(2026, 8, 30));

            row.Edit(MoneyFlowType.Income, 3, 5000m, "Salário", row.Date, included: true, Today, shiftToNextMonth: true);

            Assert.True(row.ShiftToNextMonth);
            Assert.Equal(new DateOnly(2026, 8, 30), row.Date);
            Assert.Equal(new DateOnly(2026, 9, 1), row.EffectiveDate);
            Assert.Equal("2026-09", row.Month.ToString());
        }

        [Fact]
        public void Edit_ShouldMoveADecemberRowToJanuaryOfTheNextYear_WhenShifted()
        {
            var row = Row(date: new DateOnly(2025, 12, 31));

            row.Edit(MoneyFlowType.Expense, 3, 42.9m, "Fatura", row.Date, included: true, Today, shiftToNextMonth: true);

            Assert.Equal(new DateOnly(2026, 1, 1), row.EffectiveDate);
        }

        [Fact]
        public void Edit_ShouldFollowTheNewDate_WhenTheShiftedRowGetsAnotherDate()
        {
            var row = Row(date: new DateOnly(2026, 8, 30));
            row.Edit(MoneyFlowType.Expense, 3, 42.9m, "Fatura", row.Date, included: true, Today, shiftToNextMonth: true);

            row.Edit(MoneyFlowType.Expense, 3, 42.9m, "Fatura", new DateOnly(2026, 9, 29), included: true, Today, shiftToNextMonth: true);

            Assert.Equal(new DateOnly(2026, 10, 1), row.EffectiveDate);
        }

        [Fact]
        public void Edit_ShouldBringTheRowBackToTheMonthOfItsDate_WhenNotShifted()
        {
            var row = Row(date: new DateOnly(2026, 8, 30));
            row.ApplyLearnedShift();

            row.Edit(MoneyFlowType.Expense, 3, 42.9m, "Fatura", row.Date, included: true, Today);

            Assert.False(row.ShiftToNextMonth);
            Assert.False(row.ShiftLearned);
            Assert.Equal(new DateOnly(2026, 8, 30), row.EffectiveDate);
        }

        [Fact]
        public void EditInBulk_ShouldShiftAndUnshiftTheRow()
        {
            var row = Row(date: new DateOnly(2026, 8, 30));

            row.EditInBulk(type: null, idCategory: null, included: null, shiftToNextMonth: true);
            Assert.Equal(new DateOnly(2026, 9, 1), row.EffectiveDate);

            row.EditInBulk(type: null, idCategory: null, included: null, shiftToNextMonth: false);
            Assert.Equal(new DateOnly(2026, 8, 30), row.EffectiveDate);
        }

        [Fact]
        public void EditInBulk_ShouldKeepTheShiftAndForgetThatItWasLearned_WhenTheShiftIsNotGiven()
        {
            var row = Row(date: new DateOnly(2026, 8, 30));
            row.ApplyLearnedShift();

            row.EditInBulk(type: null, idCategory: 5, included: null);

            Assert.True(row.ShiftToNextMonth);
            Assert.False(row.ShiftLearned);
            Assert.Equal(new DateOnly(2026, 9, 1), row.EffectiveDate);
        }

        [Fact]
        public void ApplyLearnedShift_ShouldShiftTheRowAndRememberWhy()
        {
            var row = Row(date: new DateOnly(2026, 8, 30));

            row.ApplyLearnedShift();

            Assert.True(row.ShiftToNextMonth);
            Assert.True(row.ShiftLearned);
            Assert.Equal(new DateOnly(2026, 9, 1), row.EffectiveDate);
            Assert.False(row.EditedByUser);
        }

        [Fact]
        public void CopyDecisionFrom_ShouldCopyTheShift()
        {
            var source = Row(date: new DateOnly(2026, 8, 30));
            source.Edit(MoneyFlowType.Income, 3, 42.9m, "Salário", source.Date, included: true, Today, shiftToNextMonth: true);
            var row = Row(date: new DateOnly(2026, 7, 31));
            row.ApplyLearnedShift();

            row.CopyDecisionFrom(source);

            Assert.True(row.ShiftToNextMonth);
            Assert.False(row.ShiftLearned);
            Assert.Equal(new DateOnly(2026, 8, 1), row.EffectiveDate);
        }

        [Theory]
        [InlineData(2026, 8, 31, true)]
        [InlineData(2026, 8, 27, true)]
        [InlineData(2026, 8, 26, false)]
        [InlineData(2026, 9, 1, false)]
        [InlineData(2026, 2, 24, true)]
        [InlineData(2026, 2, 23, false)]
        [InlineData(2028, 2, 25, true)]
        [InlineData(2028, 2, 24, false)]
        public void IsInShiftWindow_ShouldCoverTheLastFiveDaysOfTheMonth(int year, int month, int day, bool expected)
        {
            Assert.Equal(expected, StatementImportRow.IsInShiftWindow(new DateOnly(year, month, day)));
        }

        [Fact]
        public void ApplyInvestmentDetection_ShouldFileAnApplicationAsAnIncludedInvestment()
        {
            var row = Row(amount: -500m, description: "Aplicação RDB");

            row.ApplyInvestmentDetection(InvestmentMovement.Application, null);

            Assert.Equal(MoneyFlowType.Investment, row.Type);
            Assert.Equal(-500m, row.SignedAmount);
            Assert.True(row.Included);
            Assert.Null(row.CategoryId);
            Assert.Equal(SuggestionSource.InvestmentApplication, row.SuggestionSource);
            Assert.Equal(SuggestionConfidence.High, row.SuggestionConfidence);
            Assert.Equal(0, row.SuggestionUses);
            Assert.False(row.EditedByUser);
        }

        [Fact]
        public void ApplyInvestmentDetection_ShouldKeepTheCategory_WhenOneIsGiven()
        {
            var row = Row(amount: -500m, description: "Aplicação RDB");

            row.ApplyInvestmentDetection(InvestmentMovement.Application, new CategoryId(7));

            Assert.Equal(new CategoryId(7), row.CategoryId);
        }

        [Fact]
        public void ApplyInvestmentDetection_ShouldLeaveARedemptionOut()
        {
            var row = Row(amount: 610m, description: "Resgate RDB");

            row.ApplyInvestmentDetection(InvestmentMovement.Redemption, null);

            Assert.Equal(MoneyFlowType.Income, row.Type);
            Assert.Equal(610m, row.SignedAmount);
            Assert.False(row.Included);
            Assert.Null(row.CategoryId);
            Assert.Equal(SuggestionSource.InvestmentRedemption, row.SuggestionSource);
            Assert.Equal(SuggestionConfidence.High, row.SuggestionConfidence);
            Assert.Equal(0, row.SuggestionUses);
        }

        [Theory]
        [InlineData(-28, MoneyFlowType.Expense)]
        [InlineData(1000, MoneyFlowType.Income)]
        public void ApplyOwnTransferDetection_ShouldLeaveTheRowOutWithItsType(decimal amount, MoneyFlowType type)
        {
            var row = Row(amount: amount, description: "Transferência enviada pelo Pix - Fulano Beltrano Ciclano");

            row.ApplyOwnTransferDetection();

            Assert.Equal(type, row.Type);
            Assert.Equal(amount, row.SignedAmount);
            Assert.False(row.Included);
            Assert.Null(row.CategoryId);
            Assert.Equal(SuggestionSource.OwnTransfer, row.SuggestionSource);
            Assert.Equal(SuggestionConfidence.High, row.SuggestionConfidence);
            Assert.Equal(0, row.SuggestionUses);
        }
    }
}
