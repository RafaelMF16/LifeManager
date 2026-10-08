using LifeManager.Domain.Rewards.Enums;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Rewards.Interfaces
{
    public interface IRewardRepository
    {
        Task<Reward> AddAsync(Reward reward, CancellationToken cancellationToken);

        /// <summary>Archived rewards included.</summary>
        Task<Reward?> GetByIdAsync(RewardId rewardId, UserId userId, CancellationToken cancellationToken);

        /// <param name="normalizedSearch">Already normalized (see <c>SearchText.Normalize</c>); null or empty means no filter.</param>
        Task<PagedList<Reward>> GetPagedByUserIdAsync(
            UserId userId,
            PageRequest pageRequest,
            RewardStatusFilter statusFilter,
            string? normalizedSearch,
            RewardSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken);

        /// <summary>
        /// Whether another active (not archived) reward of the user has this name; case- and accent-insensitive, comparing
        /// <see cref="RewardName.NormalizedValue"/>.
        /// </summary>
        Task<bool> ExistsActiveByNameAsync(UserId userId, RewardName name, RewardId? ignoredRewardId, CancellationToken cancellationToken);

        /// <summary>Saves every field the user, archiving or restoring can change.</summary>
        Task UpdateAsync(Reward reward, CancellationToken cancellationToken);
    }
}
