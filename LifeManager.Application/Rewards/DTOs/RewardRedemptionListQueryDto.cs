using LifeManager.Domain.Shared.Paging;

namespace LifeManager.Application.Rewards.DTOs
{
    /// <summary>Query-string parameters of <c>GET /api/Rewards/Redemptions</c>: always newest first.</summary>
    public record RewardRedemptionListQueryDto
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = PageRequest.DefaultPageSize;
    }
}
