namespace LifeManager.Domain.StatementImports
{
    /// <summary>A line as read from the bank file.</summary>
    /// <param name="Amount">With the statement's sign: negative when money left the account.</param>
    /// <param name="Fitid">The bank's own id for the line (OFX <c>FITID</c>), when the file has one.</param>
    public record StatementLine(DateOnly Date, string? Description, decimal Amount, string? Fitid);
}
