namespace LifeManager.Application.StatementImports.DTOs
{
    /// <param name="SkippedCount">Lines found already posted at confirmation time.</param>
    /// <param name="Months">The months the import posted into, chronologically.</param>
    public record StatementImportCommitResultDto(int ImportedCount, int SkippedCount, int CreatedMonthCount, IReadOnlyList<StatementImportCommittedMonthDto> Months);

    /// <param name="Month"><c>yyyy-MM</c>.</param>
    public record StatementImportCommittedMonthDto(int MonthlySummaryId, string Month, bool Created);
}
