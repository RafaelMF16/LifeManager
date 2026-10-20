namespace LifeManager.Domain.StatementImports
{
    /// <param name="SkippedCount">Lines found already posted at confirmation time (the same bank line imported meanwhile).</param>
    public record StatementImportCommitOutcome(int ImportedCount, int SkippedCount);
}
