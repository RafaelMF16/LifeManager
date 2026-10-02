namespace LifeManager.Domain.Transactions
{
    /// <summary>How many transactions of each type a month has.</summary>
    public record TransactionCounts(int IncomeCount, int ExpenseCount);
}
