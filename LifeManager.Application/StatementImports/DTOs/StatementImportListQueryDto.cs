using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.StatementImports.Enums;

namespace LifeManager.Application.StatementImports.DTOs
{
    /// <summary>Query-string parameters of <c>GET /api/StatementImports</c> (drafts first, then newest first).</summary>
    public record StatementImportListQueryDto
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = PageRequest.DefaultPageSize;
    }

    /// <summary>Query-string parameters of <c>GET /api/StatementImports/{id}/Rows</c>.</summary>
    public record StatementImportRowListQueryDto
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = PageRequest.DefaultPageSize;

        /// <summary><c>yyyy-MM</c>; null or empty means every month.</summary>
        public string? Month { get; init; }

        public StatementImportRowFilter Filter { get; init; } = StatementImportRowFilter.All;
        public string? Search { get; init; }
        public StatementImportRowSortBy SortBy { get; init; } = StatementImportRowSortBy.Date;
        public SortDirection SortDirection { get; init; } = SortDirection.Asc;
    }
}
