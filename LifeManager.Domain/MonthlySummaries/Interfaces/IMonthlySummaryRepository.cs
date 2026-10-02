using LifeManager.Domain.MonthlySummaries.Enums;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.MonthlySummaries.Interfaces
{
    public interface IMonthlySummaryRepository
    {
        Task<MonthlySummary> AddAsync(MonthlySummary monthlySummary, CancellationToken cancellationToken);
        Task<MonthlySummary?> GetByIdAsync(MonthlySummaryId monthlySummaryId, UserId userId, CancellationToken cancellationToken);

        /// <param name="year">Null means every year.</param>
        Task<PagedList<MonthlySummary>> GetPagedByUserIdAsync(
            UserId userId,
            PageRequest pageRequest,
            int? year,
            BalanceFilter balanceFilter,
            MonthlySummarySortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken);

        /// <summary>The distinct years the user has monthly summaries in, newest first.</summary>
        Task<IReadOnlyList<int>> GetYearsByUserIdAsync(UserId userId, CancellationToken cancellationToken);

        /// <summary>The user's months right before and after the given period, chronologically.</summary>
        Task<MonthlySummaryNeighbors> GetNeighborsAsync(UserId userId, MonthlySummaryYear year, MonthlySummaryMonth month, CancellationToken cancellationToken);

        Task<bool> ExistsAsync(UserId userId, MonthlySummaryMonth month, MonthlySummaryYear year, CancellationToken cancellationToken);
    }
}
