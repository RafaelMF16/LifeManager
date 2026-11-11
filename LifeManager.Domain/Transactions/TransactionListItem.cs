namespace LifeManager.Domain.Transactions
{
    /// <summary>
    /// A transaction as listed: the transaction plus its category's name and, when it is linked to one, its savings
    /// goal's name, read through joins.
    /// </summary>
    public record TransactionListItem(Transaction Transaction, string CategoryName, string? SavingsGoalName = null);
}
