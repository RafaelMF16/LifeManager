using LifeManager.Domain.RecurringTransactions.Enums;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Transactions.Enums;

namespace LifeManager.Application.RecurringTransactions.DTOs
{
    /// <summary>Query-string parameters of <c>GET /api/RecurringTransactions</c>.</summary>
    public record RecurringTransactionListQueryDto
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = PageRequest.DefaultPageSize;
        public TransactionTypeFilter Type { get; init; } = TransactionTypeFilter.All;
        public RecurringTransactionStatusFilter Status { get; init; } = RecurringTransactionStatusFilter.All;
        public string? Search { get; init; }
        public RecurringTransactionSortBy SortBy { get; init; } = RecurringTransactionSortBy.NextOccurrence;
        public SortDirection SortDirection { get; init; } = SortDirection.Asc;
    }
}
