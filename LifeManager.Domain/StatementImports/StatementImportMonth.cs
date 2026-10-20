using LifeManager.Domain.Shared.ValueObjects;

namespace LifeManager.Domain.StatementImports
{
    /// <summary>A draft's rows of one month, counted, with the totals of the included ones.</summary>
    /// <param name="MonthlySummaryId">The user's month, when they already have it; null when the confirmation will open it.</param>
    public record StatementImportMonth(
        YearMonth Month,
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
