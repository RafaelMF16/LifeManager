using LifeManager.Domain.Rewards.Enums;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;

namespace LifeManager.Application.Rewards.DTOs
{
    /// <summary>Query-string parameters of <c>GET /api/Rewards</c>.</summary>
    public record RewardListQueryDto
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = PageRequest.DefaultPageSize;
        public RewardStatusFilter Status { get; init; } = RewardStatusFilter.Active;
        public string? Search { get; init; }
        public RewardSortBy SortBy { get; init; } = RewardSortBy.Cost;
        public SortDirection SortDirection { get; init; } = SortDirection.Asc;
    }
}
