using System.Globalization;
using System.Text;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Transactions.Enums;

namespace LifeManager.Application.Transactions.Export
{
    /// <summary>
    /// Turns the export's rows into CSV lines (RFC 4180, CRLF), in the conventions of a <see cref="TransactionCsvFormat"/>.
    /// Pure: no I/O. Text cells that a spreadsheet would read as a formula get a leading <c>'</c> (CSV injection), since
    /// descriptions come from the user and from the bank.
    /// </summary>
    public sealed class TransactionCsvFormatter
    {
        private const string LineEnd = "\r\n";

        /// <summary>Characters that make a spreadsheet read a cell as a formula.</summary>
        private static readonly char[] FormulaStarts = ['=', '+', '-', '@', '\t', '\r'];

        public static readonly TransactionCsvFormatter PtBr = new(
            ';',
            CultureInfo.GetCultureInfo("pt-BR"),
            "dd/MM/yyyy",
            ["Data", "Descrição", "Categoria", "Tipo", "Valor", "Mês", "Origem"],
            type => type switch
            {
                MoneyFlowType.Income => "Receita",
                MoneyFlowType.Investment => "Investimento",
                _ => "Despesa"
            },
            origin => origin switch
            {
                TransactionOrigin.Recurring => "Recorrente",
                TransactionOrigin.Import => "Importação",
                TransactionOrigin.Reward => "Resgate",
                _ => "Manual"
            });

        public static readonly TransactionCsvFormatter EnUs = new(
            ',',
            CultureInfo.InvariantCulture,
            "yyyy-MM-dd",
            ["Date", "Description", "Category", "Type", "Amount", "Month", "Source"],
            type => type switch
            {
                MoneyFlowType.Income => "Income",
                MoneyFlowType.Investment => "Investment",
                _ => "Expense"
            },
            origin => origin switch
            {
                TransactionOrigin.Recurring => "Recurring",
                TransactionOrigin.Import => "Import",
                TransactionOrigin.Reward => "Reward",
                _ => "Manual"
            });

        /// <summary>The UTF-8 byte order mark the file starts with, so Excel on Windows reads the accents.</summary>
        public static ReadOnlyMemory<byte> Preamble { get; } = Encoding.UTF8.GetPreamble();

        private readonly char _separator;
        private readonly CultureInfo _culture;
        private readonly string _dateFormat;
        private readonly string[] _headers;
        private readonly Func<MoneyFlowType, string> _typeName;
        private readonly Func<TransactionOrigin, string> _originName;

        private TransactionCsvFormatter(
            char separator,
            CultureInfo culture,
            string dateFormat,
            string[] headers,
            Func<MoneyFlowType, string> typeName,
            Func<TransactionOrigin, string> originName)
        {
            _separator = separator;
            _culture = culture;
            _dateFormat = dateFormat;
            _headers = headers;
            _typeName = typeName;
            _originName = originName;
        }

        public static TransactionCsvFormatter For(TransactionCsvFormat format)
            => format == TransactionCsvFormat.EnUs ? EnUs : PtBr;

        /// <summary>The header line, ending with CRLF.</summary>
        public string Header()
            => string.Join(_separator, _headers.Select(Field)) + LineEnd;

        /// <summary>One transaction's line, ending with CRLF. The amount keeps its sign and two decimals, with no thousands separator.</summary>
        public string Line(TransactionExportRow row)
        {
            string[] cells =
            [
                row.Date.ToString(_dateFormat, CultureInfo.InvariantCulture),
                Text(row.Description),
                Text(row.CategoryName),
                _typeName(row.Type),
                row.SignedAmount.ToString("0.00", _culture),
                row.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                _originName(row.Origin),
            ];

            return string.Join(_separator, cells.Select(Field)) + LineEnd;
        }

        /// <summary>A text cell from outside (description, category): a leading formula character is neutralized.</summary>
        private static string Text(string value)
            => value.Length > 0 && Array.IndexOf(FormulaStarts, value[0]) >= 0 ? "'" + value : value;

        /// <summary>Quotes the cell when it holds the separator, a quote or a line break, doubling its quotes.</summary>
        private string Field(string value)
            => value.IndexOf(_separator) >= 0 || value.IndexOfAny(['"', '\r', '\n']) >= 0
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
    }
}
