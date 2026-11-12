using LifeManager.Domain.Shared.Paging;

namespace LifeManager.Application.SavingsGoals.DTOs
{
    /// <summary>Query-string parameters of <c>GET /api/SavingsGoals/{id}/Transactions</c> (always newest first).</summary>
    public record SavingsGoalTransactionListQueryDto
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = PageRequest.DefaultPageSize;
    }
}
