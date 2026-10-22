using LifeManager.Domain.StatementImports;
using LifeManager.Domain.Transactions;

namespace LifeManager.Domain.Test.StatementImports
{
    public class DuplicateDetectorTests
    {
        private static readonly DateOnly Today = new(2026, 10, 19);
        private const string Account = "account";

        private static StatementImportRow Row(DateOnly date, decimal amount, string description, string? fitid = null)
            => StatementImportRow.Create(new StatementLine(date, description, amount, fitid), Account, Today).Value!;

        private static ExistingTransaction Existing(int id, DateOnly date, decimal signedAmount, string normalizedDescription, string? externalId = null)
            => new(id, MonthlySummaryId: 50 + id, date, signedAmount, normalizedDescription, externalId);

        private static readonly DateOnly May3 = new(2024, 5, 3);

        [Fact]
        public void MarkDuplicates_ShouldMatchTheSameBankLine_WhateverTheDateAndAmount()
        {
            var row = Row(May3, -10m, "NETFLIX", fitid: "F1");
            var existing = Existing(1, May3.AddDays(-20), -99m, "outra coisa", ImportExternalId.From(Account, "F1"));

            DuplicateDetector.MarkDuplicates([row], [existing]);

            Assert.Equal(1, row.DuplicateOfTransactionId);
            Assert.Equal(51, row.DuplicateOfMonthlySummaryId);
            Assert.False(row.Included);
        }

        [Fact]
        public void MarkDuplicates_ShouldMatchAShiftedRowOnTheFirstDayOfTheNextMonth()
        {
            var row = Row(new DateOnly(2024, 8, 30), 5000m, "PIX RECEBIDO EMPRESA XPTO");
            row.ApplyLearnedShift();

            DuplicateDetector.MarkDuplicates([row], [Existing(1, new DateOnly(2024, 9, 1), 5000m, "salario")]);

            Assert.Equal(1, row.DuplicateOfTransactionId);
        }

        [Fact]
        public void MarkDuplicates_ShouldNotMatchTheNextMonthsFirstDay_WhenTheRowIsNotShifted()
        {
            var row = Row(new DateOnly(2024, 8, 30), 5000m, "PIX RECEBIDO EMPRESA XPTO");

            DuplicateDetector.MarkDuplicates([row], [Existing(1, new DateOnly(2024, 9, 1), 5000m, "salario")]);

            Assert.False(row.IsDuplicate);
        }

        [Fact]
        public void MarkDuplicates_ShouldMatchATransactionTypedByHand_OnTheSameDateAndAmount()
        {
            var row = Row(May3, -150m, "PIX ENVIADO JOAO DA SILVA");

            DuplicateDetector.MarkDuplicates([row], [Existing(1, May3, -150m, "aluguel")]);

            Assert.Equal(1, row.DuplicateOfTransactionId);
        }

        [Fact]
        public void MarkDuplicates_ShouldNotMatch_WhenTheDirectionDiffers()
        {
            var row = Row(May3, 150m, "PIX RECEBIDO JOAO");

            DuplicateDetector.MarkDuplicates([row], [Existing(1, May3, -150m, "pix recebido joao")]);

            Assert.False(row.IsDuplicate);
        }

        [Fact]
        public void MarkDuplicates_ShouldMatchAFewDaysApart_OnlyWithASimilarMerchant()
        {
            var similar = Row(May3, -59.9m, "COMPRA CARTAO SPOTIFY");
            var different = Row(May3, -30m, "COMPRA CARTAO PADARIA");

            DuplicateDetector.MarkDuplicates(
                [similar, different],
                [Existing(1, May3.AddDays(-2), -59.9m, "spotify"), Existing(2, May3.AddDays(1), -30m, "academia")]);

            Assert.Equal(1, similar.DuplicateOfTransactionId);
            Assert.False(different.IsDuplicate);
        }

        [Fact]
        public void MarkDuplicates_ShouldNotMatchFurtherThanAFewDays()
        {
            var row = Row(May3, -59.9m, "SPOTIFY");

            DuplicateDetector.MarkDuplicates([row], [Existing(1, May3.AddDays(-DuplicateDetector.NearbyDays - 1), -59.9m, "spotify")]);

            Assert.False(row.IsDuplicate);
        }

        [Fact]
        public void MarkDuplicates_ShouldMatchEachTransactionOnce_SoRepeatedLinesStayApart()
        {
            var firstCoffee = Row(May3, -8m, "CAFETERIA");
            var secondCoffee = Row(May3, -8m, "CAFETERIA");

            DuplicateDetector.MarkDuplicates([firstCoffee, secondCoffee], [Existing(1, May3, -8m, "cafe")]);

            Assert.True(firstCoffee.IsDuplicate);
            Assert.False(secondCoffee.IsDuplicate);
        }

        [Fact]
        public void MarkDuplicates_ShouldPreferTheClosestDescription_OnTheSameDay()
        {
            var row = Row(May3, -20m, "UBER TRIP");

            DuplicateDetector.MarkDuplicates([row], [Existing(1, May3, -20m, "farmacia"), Existing(2, May3, -20m, "uber trip")]);

            Assert.Equal(2, row.DuplicateOfTransactionId);
        }

        [Fact]
        public void MarkDuplicates_ShouldNotReuseATransactionMatchedByItsBankLine()
        {
            var imported = Row(May3, -8m, "CAFETERIA", fitid: "F1");
            var other = Row(May3, -8m, "CAFETERIA", fitid: "F2");

            DuplicateDetector.MarkDuplicates([imported, other], [Existing(1, May3, -8m, "cafeteria", ImportExternalId.From(Account, "F1"))]);

            Assert.True(imported.IsDuplicate);
            Assert.False(other.IsDuplicate);
        }
    }
}
