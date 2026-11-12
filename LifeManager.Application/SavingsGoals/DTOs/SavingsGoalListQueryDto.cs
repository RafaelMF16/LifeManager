using LifeManager.Domain.SavingsGoals.Enums;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;

namespace LifeManager.Application.SavingsGoals.DTOs
{
    /// <summary>Query-string parameters of <c>GET /api/SavingsGoals</c>.</summary>
    public record SavingsGoalListQueryDto
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = PageRequest.DefaultPageSize;
        public SavingsGoalStatusFilter Status { get; init; } = SavingsGoalStatusFilter.Active;
        public string? Search { get; init; }
        public SavingsGoalSortBy SortBy { get; init; } = SavingsGoalSortBy.Name;
        public SortDirection SortDirection { get; init; } = SortDirection.Asc;
    }
}
