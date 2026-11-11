namespace LifeManager.Domain.Transactions.Enums
{
    /// <summary>Where a transaction came from, as the export shows it.</summary>
    public enum TransactionOrigin
    {
        /// <summary>Typed by the user (also a recurrence's or an import's posting whose link was cleared).</summary>
        Manual = 1,

        /// <summary>Posted by a recurring transaction (<see cref="Transaction.RecurringTransactionId"/>).</summary>
        Recurring = 2,

        /// <summary>Posted by a bank statement import (<see cref="Transaction.StatementImportId"/> or its bank line).</summary>
        Import = 3,

        /// <summary>The real expense of a redeemed reward (a redemption points to it).</summary>
        Reward = 4
    }
}
