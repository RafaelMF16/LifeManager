using System.Text;
using LifeManager.Application.Transactions.Export;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Transactions.Enums;

namespace LifeManager.Application.Test.Transactions
{
    /// <summary>The pure CSV lines of the transactions export: no I/O, no container.</summary>
    public class TransactionCsvFormatterTest
    {
        private static readonly DateOnly Date = new(2026, 3, 7);

        [Fact]
        public void Header_ShouldBePortugueseWithSemicolons_WhenFormatIsPtBr()
        {
            Assert.Equal("Data;Descrição;Categoria;Tipo;Valor;Mês;Origem\r\n", TransactionCsvFormatter.PtBr.Header());
        }

        [Fact]
        public void Header_ShouldBeEnglishWithCommas_WhenFormatIsEnUs()
        {
            Assert.Equal("Date,Description,Category,Type,Amount,Month,Source\r\n", TransactionCsvFormatter.EnUs.Header());
        }

        [Fact]
        public void Line_ShouldUseDecimalCommaAndBrazilianDate_WhenFormatIsPtBr()
        {
            var line = TransactionCsvFormatter.PtBr.Line(Row("Supermercado", "Mercado", MoneyFlowType.Expense, -1234.5m, TransactionOrigin.Manual));

            Assert.Equal("07/03/2026;Supermercado;Mercado;Despesa;-1234,50;2026-03;Manual\r\n", line);
        }

        [Fact]
        public void Line_ShouldUseDecimalPointAndIsoDate_WhenFormatIsEnUs()
        {
            var line = TransactionCsvFormatter.EnUs.Line(Row("Salary", "Salary", MoneyFlowType.Income, 5000m, TransactionOrigin.Recurring));

            Assert.Equal("2026-03-07,Salary,Salary,Income,5000.00,2026-03,Recurring\r\n", line);
        }

        [Theory]
        [InlineData(MoneyFlowType.Expense, -10, "Despesa", "Expense", "-10,00", "-10.00")]
        [InlineData(MoneyFlowType.Income, 10, "Receita", "Income", "10,00", "10.00")]
        [InlineData(MoneyFlowType.Investment, -10, "Investimento", "Investment", "-10,00", "-10.00")]
        public void Line_ShouldWriteTheTypeAndTheSignedAmount_ForEveryType(
            MoneyFlowType type, int signedAmount, string ptBrType, string enUsType, string ptBrAmount, string enUsAmount)
        {
            var row = Row("Algo", "Cat", type, signedAmount, TransactionOrigin.Manual);

            Assert.Equal(["07/03/2026", "Algo", "Cat", ptBrType, ptBrAmount, "2026-03", "Manual"], Cells(TransactionCsvFormatter.PtBr.Line(row), ';'));
            Assert.Equal(["2026-03-07", "Algo", "Cat", enUsType, enUsAmount, "2026-03", "Manual"], Cells(TransactionCsvFormatter.EnUs.Line(row), ','));
        }

        [Theory]
        [InlineData(TransactionOrigin.Manual, "Manual", "Manual")]
        [InlineData(TransactionOrigin.Recurring, "Recorrente", "Recurring")]
        [InlineData(TransactionOrigin.Import, "Importação", "Import")]
        [InlineData(TransactionOrigin.Reward, "Resgate", "Reward")]
        public void Line_ShouldNameTheOrigin_InBothLanguages(TransactionOrigin origin, string ptBr, string enUs)
        {
            var row = Row("Algo", "Cat", MoneyFlowType.Expense, -1m, origin);

            Assert.EndsWith($";{ptBr}\r\n", TransactionCsvFormatter.PtBr.Line(row));
            Assert.EndsWith($",{enUs}\r\n", TransactionCsvFormatter.EnUs.Line(row));
        }

        [Fact]
        public void Line_ShouldQuoteTheField_WhenItHoldsTheSeparatorOrQuotes()
        {
            var row = Row("Pão; leite, \"o bom\"", "Mercado", MoneyFlowType.Expense, -5m, TransactionOrigin.Manual);

            Assert.Equal("07/03/2026;\"Pão; leite, \"\"o bom\"\"\";Mercado;Despesa;-5,00;2026-03;Manual\r\n", TransactionCsvFormatter.PtBr.Line(row));
            Assert.Equal("2026-03-07,\"Pão; leite, \"\"o bom\"\"\",Mercado,Expense,-5.00,2026-03,Manual\r\n", TransactionCsvFormatter.EnUs.Line(row));
        }

        [Fact]
        public void Line_ShouldNotQuote_WhenTheOtherFormatsSeparatorIsInTheText()
        {
            var row = Row("A, B", "Cat", MoneyFlowType.Expense, -5m, TransactionOrigin.Manual);

            Assert.StartsWith("07/03/2026;A, B;", TransactionCsvFormatter.PtBr.Line(row));
        }

        [Fact]
        public void Line_ShouldQuoteTheField_WhenItHoldsALineBreak()
        {
            var row = Row("Linha 1\nLinha 2", "Cat", MoneyFlowType.Expense, -5m, TransactionOrigin.Manual);

            Assert.StartsWith("07/03/2026;\"Linha 1\nLinha 2\";", TransactionCsvFormatter.PtBr.Line(row));
        }

        [Theory]
        [InlineData("=SOMA(A1:A9)", "'=SOMA(A1:A9)")]
        [InlineData("+5511999", "'+5511999")]
        [InlineData("-cmd", "'-cmd")]
        [InlineData("@SUM(1)", "'@SUM(1)")]
        [InlineData("\tTab", "'\tTab")]
        [InlineData("Uber =taxi", "Uber =taxi")]
        public void Line_ShouldNeutralizeAFormula_InTheDescriptionAndTheCategory(string text, string expected)
        {
            var row = Row(text, text, MoneyFlowType.Expense, -5m, TransactionOrigin.Manual);

            var cells = Cells(TransactionCsvFormatter.PtBr.Line(row), ';');

            Assert.Equal(expected, cells[1]);
            Assert.Equal(expected, cells[2]);
        }

        [Fact]
        public void Line_ShouldQuoteAndNeutralize_WhenAFormulaHoldsTheSeparator()
        {
            var row = Row("=HYPERLINK(\"x\";\"y\")", "Cat", MoneyFlowType.Expense, -5m, TransactionOrigin.Manual);

            Assert.StartsWith("07/03/2026;\"'=HYPERLINK(\"\"x\"\";\"\"y\"\")\";", TransactionCsvFormatter.PtBr.Line(row));
        }

        [Fact]
        public void Line_ShouldKeepTheMinusOfANegativeAmount_WithoutNeutralizingIt()
        {
            var cells = Cells(TransactionCsvFormatter.EnUs.Line(Row("A", "B", MoneyFlowType.Expense, -0.5m, TransactionOrigin.Manual)), ',');

            Assert.Equal("-0.50", cells[4]);
        }

        [Fact]
        public void Preamble_ShouldBeTheUtf8ByteOrderMark()
        {
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, TransactionCsvFormatter.Preamble.ToArray());
            Assert.Equal(Encoding.UTF8.GetPreamble(), TransactionCsvFormatter.Preamble.ToArray());
        }

        [Fact]
        public void For_ShouldPickTheFormatter_OfEachFormat()
        {
            Assert.Same(TransactionCsvFormatter.PtBr, TransactionCsvFormatter.For(TransactionCsvFormat.PtBr));
            Assert.Same(TransactionCsvFormatter.EnUs, TransactionCsvFormatter.For(TransactionCsvFormat.EnUs));
        }

        private static TransactionExportRow Row(string description, string category, MoneyFlowType type, decimal signedAmount, TransactionOrigin origin)
            => new(Date, description, category, type, signedAmount, origin);

        /// <summary>Splits a line without quoted fields.</summary>
        private static string[] Cells(string line, char separator)
            => line.TrimEnd('\r', '\n').Split(separator);
    }
}
