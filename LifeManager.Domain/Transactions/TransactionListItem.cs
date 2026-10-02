namespace LifeManager.Domain.Transactions
{
    /// <summary>A transaction as listed: the transaction plus its category's name, read through a join.</summary>
    public record TransactionListItem(Transaction Transaction, string CategoryName);
}
