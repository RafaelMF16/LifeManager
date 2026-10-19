using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Application.StatementImports.DTOs
{
    /// <summary>Body of <c>PUT /api/StatementImports/{id}/Rows/{rowId}</c>.</summary>
    /// <param name="CategoryId">Null leaves the row without a category (it can't be confirmed while included).</param>
    public record StatementImportRowDto(MoneyFlowType Type, int? CategoryId, decimal Amount, string Description, DateOnly Date, bool Included);

    /// <summary>Body of <c>POST /api/StatementImports/{id}/Rows/Bulk</c>: only the given values change.</summary>
    public record StatementImportBulkUpdateDto(IReadOnlyList<int>? RowIds, MoneyFlowType? Type, int? CategoryId, bool? Included);
}
