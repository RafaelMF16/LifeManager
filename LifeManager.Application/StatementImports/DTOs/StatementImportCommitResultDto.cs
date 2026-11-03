using LifeManager.Application.Habits.DTOs;

namespace LifeManager.Application.StatementImports.DTOs
{
    /// <param name="SkippedCount">Lines found already posted at confirmation time.</param>
    /// <param name="Months">The months the import posted into, chronologically.</param>
    /// <param name="AutoCheckIns">The habits the commit checked in by themselves; empty when none.</param>
    public record StatementImportCommitResultDto(
        int ImportedCount,
        int SkippedCount,
        int CreatedMonthCount,
        IReadOnlyList<StatementImportCommittedMonthDto> Months,
        IReadOnlyList<AutoCheckInDto> AutoCheckIns);

    /// <param name="Month"><c>yyyy-MM</c>.</param>
    public record StatementImportCommittedMonthDto(int MonthlySummaryId, string Month, bool Created);
}
