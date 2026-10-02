using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Transactions.Enums;

namespace LifeManager.Application.Transactions.DTOs
{
    /// <summary>Query-string parameters of <c>GET /api/MonthlySummaries/{monthlySummaryId}/Transactions</c>.</summary>
    public record TransactionListQueryDto
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = PageRequest.DefaultPageSize;
        public TransactionTypeFilter Type { get; init; } = TransactionTypeFilter.All;

        /// <summary>Null means every category.</summary>
        public int? CategoryId { get; init; }

        public string? Search { get; init; }
        public TransactionSortBy SortBy { get; init; } = TransactionSortBy.Date;
        public SortDirection SortDirection { get; init; } = SortDirection.Desc;
    }
}
