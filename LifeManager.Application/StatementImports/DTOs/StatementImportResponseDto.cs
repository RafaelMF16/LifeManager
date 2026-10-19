using LifeManager.Domain.StatementImports.Enums;

namespace LifeManager.Application.StatementImports.DTOs
{
    /// <summary>An import as listed in the history (and returned when a draft is created).</summary>
    public record StatementImportResponseDto(
        int Id,
        string FileName,
        StatementImportSource Source,
        StatementImportStatus Status,
        int RowCount,
        DateOnly FirstDate,
        DateOnly LastDate,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CommittedAt,
        int? ImportedCount,
        int? CreatedMonthCount);
}
