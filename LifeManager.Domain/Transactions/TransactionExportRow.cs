using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Transactions.Enums;

namespace LifeManager.Domain.Transactions
{
    /// <summary>A transaction as the CSV export writes it: plain values, the category's name and where it came from.</summary>
    /// <param name="SignedAmount">Income positive; expense and investment negative (<see cref="Transaction.SignedAmount"/>).</param>
    public record TransactionExportRow(
        DateOnly Date,
        string Description,
        string CategoryName,
        MoneyFlowType Type,
        decimal SignedAmount,
        TransactionOrigin Origin);
}
