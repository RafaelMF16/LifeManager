using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.StatementImports;
using LifeManager.Domain.StatementImports.Errors;
using LifeManager.Domain.StatementImports.ValueObjects;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Transactions.Errors;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.StatementImports
{
    public class ImportPostingTests
    {
        private static readonly DateOnly Today = new(2026, 10, 19);

        private static MonthlySummary PastMonth(int year, int month)
        {
            YearMonth.TryParse($"{year:D4}-{month:D2}", out var yearMonth);
            var monthlySummary = MonthlySummary.OpenForImport(new UserId(1), yearMonth);
            monthlySummary.AssignId(9);

            return monthlySummary;
        }

        private static StatementImportRow Row(DateOnly date, int? categoryId)
        {
            var row = StatementImportRow.Create(new StatementLine(date, "COMPRA CARTAO MERCADO", -80.5m, "F1"), "account", Today).Value!;
            row.Edit(MoneyFlowType.Expense, categoryId, 80.5m, "Mercado", date, included: true, Today);
            row.AssignImport(new StatementImportId(3));

            return row;
        }

        [Fact]
        public void OpenForImport_ShouldOpenAMonthOfAPastYearWithZeroTotals()
        {
            var monthlySummary = PastMonth(2019, 2);

            Assert.Equal(2019, monthlySummary.Year.Value);
            Assert.Equal(2, monthlySummary.Month.Value);
            Assert.Equal(0, monthlySummary.TotalIncome.Value);
            Assert.Equal(0, monthlySummary.BalanceAmount);
        }

        [Fact]
        public void CreateFromImport_ShouldPostTheRowAndRememberItsImportAndBankLine()
        {
            var row = Row(new DateOnly(2019, 2, 14), categoryId: 6);

            var result = Transaction.CreateFromImport(row, PastMonth(2019, 2));

            Assert.True(result.IsSuccess);
            var transaction = result.Value;
            Assert.Equal(MoneyFlowType.Expense, transaction.Type);
            Assert.Equal(6, transaction.CategoryId.Value);
            Assert.Equal(80.5m, transaction.Amount.Value);
            Assert.Equal("Mercado", transaction.Description.Value);
            Assert.Equal(new DateOnly(2019, 2, 14), transaction.TransactionDate);
            Assert.Equal(9, transaction.MonthlySummaryId.Value);
            Assert.Equal(new StatementImportId(3), transaction.StatementImportId);
            Assert.Equal(row.ExternalId, transaction.ExternalId);
            Assert.Null(transaction.RecurringTransactionId);
        }

        [Fact]
        public void CreateFromImport_ShouldFail_WhenTheRowHasNoCategory()
        {
            var result = Transaction.CreateFromImport(Row(new DateOnly(2019, 2, 14), categoryId: null), PastMonth(2019, 2));

            Assert.Equal(StatementImportErrors.UncategorizedRows, result.Error);
        }

        [Fact]
        public void CreateFromImport_ShouldFail_WhenTheMonthIsNotTheRowsMonth()
        {
            var result = Transaction.CreateFromImport(Row(new DateOnly(2019, 3, 1), categoryId: 6), PastMonth(2019, 2));

            Assert.Equal(TransactionErrors.DateOutsideMonth, result.Error);
        }
    }
}
