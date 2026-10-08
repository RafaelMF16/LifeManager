using LifeManager.Domain.Rewards;
using LifeManager.Domain.Rewards.Enums;
using LifeManager.Domain.Rewards.Interfaces;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Rewards
{
    public class RewardRepository(LifeManagerDbContext dbContext) : IRewardRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<Reward> AddAsync(Reward reward, CancellationToken cancellationToken)
        {
            _dbContext.Add(reward);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return reward;
        }

        public async Task<Reward?> GetByIdAsync(RewardId rewardId, UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.Rewards
                .AsNoTracking()
                .SingleOrDefaultAsync(reward => reward.Id == rewardId && reward.UserId == userId, cancellationToken);
        }

        public async Task<PagedList<Reward>> GetPagedByUserIdAsync(
            UserId userId,
            PageRequest pageRequest,
            RewardStatusFilter statusFilter,
            string? normalizedSearch,
            RewardSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken)
        {
            var query = _dbContext.Rewards
                .AsNoTracking()
                .Where(reward => reward.UserId == userId);

            query = statusFilter switch
            {
                RewardStatusFilter.Archived => query.Where(reward => reward.ArchivedAt != null),
                _ => query.Where(reward => reward.ArchivedAt == null)
            };

            if (!string.IsNullOrEmpty(normalizedSearch))
            {
                var pattern = QueryablePagingExtensions.ToContainsLikePattern(normalizedSearch);
                query = query.Where(reward => EF.Functions.Like(reward.NormalizedName, pattern, QueryablePagingExtensions.LikeEscapeCharacter));
            }

            return await Order(query, sortBy, sortDirection).ToPagedListAsync(pageRequest, cancellationToken);
        }

        public async Task<bool> ExistsActiveByNameAsync(UserId userId, RewardName name, RewardId? ignoredRewardId, CancellationToken cancellationToken)
        {
            var normalizedName = name.NormalizedValue;
            var query = _dbContext.Rewards
                .Where(reward => reward.UserId == userId && reward.NormalizedName == normalizedName && reward.ArchivedAt == null);

            if (ignoredRewardId is not null)
                query = query.Where(reward => reward.Id != ignoredRewardId);

            return await query.AnyAsync(cancellationToken);
        }

        public async Task UpdateAsync(Reward reward, CancellationToken cancellationToken)
        {
            await _dbContext.Rewards
                .Where(storedReward => storedReward.Id == reward.Id && storedReward.UserId == reward.UserId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(storedReward => storedReward.Name, reward.Name)
                    .SetProperty(storedReward => storedReward.NormalizedName, reward.NormalizedName)
                    .SetProperty(storedReward => storedReward.Cost, reward.Cost)
                    .SetProperty(storedReward => storedReward.Icon, reward.Icon)
                    .SetProperty(storedReward => storedReward.ArchivedAt, reward.ArchivedAt), cancellationToken);
        }

        /// <summary>Sorts by the chosen column, then by name, ending with Id so pages are stable.</summary>
        private static IOrderedQueryable<Reward> Order(IQueryable<Reward> query, RewardSortBy sortBy, SortDirection sortDirection)
        {
            var descending = sortDirection == SortDirection.Desc;

            var ordered = sortBy switch
            {
                RewardSortBy.Cost => descending
                    ? query.OrderByDescending(reward => reward.Cost)
                    : query.OrderBy(reward => reward.Cost),
                _ => descending
                    ? query.OrderByDescending(reward => reward.NormalizedName)
                    : query.OrderBy(reward => reward.NormalizedName)
            };

            return descending
                ? ordered.ThenByDescending(reward => reward.NormalizedName).ThenByDescending(reward => reward.Id)
                : ordered.ThenBy(reward => reward.NormalizedName).ThenBy(reward => reward.Id);
        }
    }
}
