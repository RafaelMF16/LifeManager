namespace LifeManager.Domain.RecurringTransactions.Enums
{
    public enum RecurringTransactionStatus
    {
        /// <summary>Posts its next occurrence when the day comes.</summary>
        Active = 1,

        /// <summary>Stopped by the user; the months it stays paused are skipped.</summary>
        Paused = 2,

        /// <summary>Past its end month: nothing is left to post.</summary>
        Finished = 3
    }
}
