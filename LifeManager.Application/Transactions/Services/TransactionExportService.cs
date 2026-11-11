using System.Text;
using LifeManager.Application.Transactions.DTOs;
using LifeManager.Application.Transactions.Export;
using LifeManager.Domain.FinanceDashboard.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Transactions.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Transactions.Services
{
    /// <summary>
    /// The CSV export of a period's transactions. <see cref="PrepareAsync"/> validates the period (the dashboard's rules
    /// and error codes) and counts the rows before anything is sent; <see cref="WriteAsync"/> then streams the file.
    /// </summary>
    public class TransactionExportService(ITransactionRepository transactionRepository)
    {
        // Sent to the output once the buffer holds this many characters, so a long period never piles up in memory.
        private const int FlushThresholdChars = 16 * 1024;

        private readonly ITransactionRepository _transactionRepository = transactionRepository;

        public async Task<Result<TransactionExport>> PrepareAsync(UserId userId, TransactionExportQueryDto query, CancellationToken cancellationToken)
        {
            var periodResult = DashboardPeriod.Create(query.From, query.To);
            if (!periodResult.IsSuccess)
                return periodResult.Error;

            var period = periodResult.Value!;
            var rowCount = await _transactionRepository.CountForExportAsync(userId, period.FirstDay, period.LastDay, query.Type, cancellationToken);

            return new TransactionExport(userId, period.FirstDay, period.LastDay, query.Type, query.Format, rowCount, FileName(period, query.Format));
        }

        /// <summary>
        /// Writes the BOM, the header and one line per transaction (by date, then id) to <paramref name="output"/>, only
        /// asynchronously (Kestrel's body refuses synchronous writes).
        /// </summary>
        public async Task WriteAsync(TransactionExport export, Stream output, CancellationToken cancellationToken)
        {
            var formatter = TransactionCsvFormatter.For(export.Format);
            var buffer = new StringBuilder(formatter.Header());

            await output.WriteAsync(TransactionCsvFormatter.Preamble, cancellationToken);

            var rows = _transactionRepository.StreamForExportAsync(export.UserId, export.From, export.To, export.Type, cancellationToken);
            await foreach (var row in rows.WithCancellation(cancellationToken))
            {
                buffer.Append(formatter.Line(row));
                if (buffer.Length >= FlushThresholdChars)
                    await SendAsync(buffer, output, cancellationToken);
            }

            await SendAsync(buffer, output, cancellationToken);
        }

        /// <summary><c>lancamentos-2026-01_2026-11.csv</c> (<c>transactions-…</c> in English).</summary>
        private static string FileName(DashboardPeriod period, TransactionCsvFormat format)
            => $"{(format == TransactionCsvFormat.EnUs ? "transactions" : "lancamentos")}-{period.From}_{period.To}.csv";

        private static async Task SendAsync(StringBuilder buffer, Stream output, CancellationToken cancellationToken)
        {
            if (buffer.Length == 0)
                return;

            await output.WriteAsync(Encoding.UTF8.GetBytes(buffer.ToString()), cancellationToken);
            buffer.Clear();
        }
    }
}
