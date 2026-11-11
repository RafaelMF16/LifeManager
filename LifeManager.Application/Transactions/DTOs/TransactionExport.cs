using LifeManager.Application.Transactions.Export;
using LifeManager.Domain.Transactions.Enums;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Transactions.DTOs
{
    /// <summary>
    /// A validated export, ready to be written (<see cref="Services.TransactionExportService.WriteAsync"/>): the
    /// response's headers come from it before the first byte of the file.
    /// </summary>
    /// <param name="RowCount">How many transactions the file has (the <c>X-Row-Count</c> header).</param>
    public record TransactionExport(
        UserId UserId,
        DateOnly From,
        DateOnly To,
        TransactionTypeFilter Type,
        TransactionCsvFormat Format,
        int RowCount,
        string FileName);
}
