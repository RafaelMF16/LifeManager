using LifeManager.Domain.MonthlySummaries.Enums;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;

namespace LifeManager.Application.MonthlySummaries.DTOs
{
    /// <summary>Query-string parameters of <c>GET /api/MonthlySummaries</c>.</summary>
    public record MonthlySummaryListQueryDto
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = PageRequest.DefaultPageSize;
        public int? Year { get; init; }
        public BalanceFilter Balance { get; init; } = BalanceFilter.All;
        public MonthlySummarySortBy SortBy { get; init; } = MonthlySummarySortBy.Period;
        public SortDirection SortDirection { get; init; } = SortDirection.Desc;
    }
}
