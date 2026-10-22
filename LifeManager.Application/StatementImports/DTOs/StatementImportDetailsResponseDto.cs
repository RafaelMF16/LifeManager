using LifeManager.Domain.StatementImports.Enums;

namespace LifeManager.Application.StatementImports.DTOs
{
    /// <summary>
    /// <c>GET /api/StatementImports/{id}</c>: the import, and while it is a draft its months (empty once confirmed) and
    /// the counts over all of them.
    /// </summary>
    public record StatementImportDetailsResponseDto(
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
        int? CreatedMonthCount,
        int IncludedCount,
        int UncategorizedCount,
        int SuggestedCount,
        int DuplicateCount,
        IReadOnlyList<StatementImportMonthDto> Months);

    /// <param name="Month"><c>yyyy-MM</c>.</param>
    /// <param name="MonthlySummaryId">The user's month when it already exists; null when the confirmation will open it.</param>
    public record StatementImportMonthDto(
        string Month,
        int? MonthlySummaryId,
        int RowCount,
        int IncludedCount,
        int UncategorizedCount,
        int SuggestedCount,
        int DuplicateCount,
        decimal TotalIncome,
        decimal TotalExpense,
        decimal TotalInvestment);
}
