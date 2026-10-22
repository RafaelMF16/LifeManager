namespace LifeManager.Domain.Transactions
{
    /// <summary>What an import compares a statement line against to tell whether the user already has it.</summary>
    public record ExistingTransaction(
        int Id,
        int MonthlySummaryId,
        DateOnly Date,
        decimal SignedAmount,
        string NormalizedDescription,
        string? ExternalId);
}
