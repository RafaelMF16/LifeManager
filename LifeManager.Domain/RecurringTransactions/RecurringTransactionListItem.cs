namespace LifeManager.Domain.RecurringTransactions
{
    /// <summary>A recurring transaction as listed: the recurrence plus its category's name, read through a join.</summary>
    public record RecurringTransactionListItem(RecurringTransaction RecurringTransaction, string CategoryName);
}
