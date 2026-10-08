using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Rewards;
using LifeManager.Domain.Rewards.Enums;
using LifeManager.Domain.Rewards.Interfaces;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Rewards.Mocks
{
    public class RewardRepositoryMock : IRewardRepository
    {
        private readonly RewardSingleton _instance = RewardSingleton.Instance;

        public Task<Reward> AddAsync(Reward reward, CancellationToken cancellationToken)
        {
            var newId = _instance.Count == 0 ? 1 : _instance.Max(storedReward => storedReward.Id!.Value) + 1;
            reward.AssignId(newId);

            _instance.Add(ToDetachedCopy(reward));

            return Task.FromResult(reward);
        }

        public Task<Reward?> GetByIdAsync(RewardId rewardId, UserId userId, CancellationToken cancellationToken)
        {
            var storedReward = _instance.SingleOrDefault(reward => reward.Id == rewardId && reward.UserId == userId);

            return Task.FromResult(storedReward is null ? null : ToDetachedCopy(storedReward));
        }

        public Task<PagedList<Reward>> GetPagedByUserIdAsync(
            UserId userId,
            PageRequest pageRequest,
            RewardStatusFilter statusFilter,
            string? normalizedSearch,
            RewardSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken)
        {
            var archived = statusFilter == RewardStatusFilter.Archived;
            var query = _instance.Where(reward => reward.UserId == userId && reward.IsArchived == archived);

            if (!string.IsNullOrEmpty(normalizedSearch))
                query = query.Where(reward => reward.NormalizedName.Contains(normalizedSearch, StringComparison.Ordinal));

            var descending = sortDirection == SortDirection.Desc;
            var ordered = sortBy switch
            {
                RewardSortBy.Cost => descending
                    ? query.OrderByDescending(reward => reward.Cost)
                    : query.OrderBy(reward => reward.Cost),
                _ => descending
                    ? query.OrderByDescending(reward => reward.NormalizedName, StringComparer.Ordinal)
                    : query.OrderBy(reward => reward.NormalizedName, StringComparer.Ordinal)
            };
            ordered = descending
                ? ordered.ThenByDescending(reward => reward.NormalizedName, StringComparer.Ordinal).ThenByDescending(reward => reward.Id!.Value)
                : ordered.ThenBy(reward => reward.NormalizedName, StringComparer.Ordinal).ThenBy(reward => reward.Id!.Value);

            var matching = ordered.ToList();
            IReadOnlyList<Reward> items = [.. matching.Skip(pageRequest.Skip).Take(pageRequest.PageSize).Select(ToDetachedCopy)];

            return Task.FromResult(new PagedList<Reward>(items, matching.Count, pageRequest.Page, pageRequest.PageSize));
        }

        public Task<bool> ExistsActiveByNameAsync(UserId userId, RewardName name, RewardId? ignoredRewardId, CancellationToken cancellationToken)
        {
            var exists = _instance.Any(reward =>
                reward.UserId == userId
                && !reward.IsArchived
                && reward.NormalizedName == name.NormalizedValue
                && (ignoredRewardId is null || reward.Id != ignoredRewardId));

            return Task.FromResult(exists);
        }

        public Task UpdateAsync(Reward reward, CancellationToken cancellationToken)
        {
            var index = _instance.FindIndex(storedReward => storedReward.Id == reward.Id && storedReward.UserId == reward.UserId);
            if (index >= 0)
                _instance[index] = ToDetachedCopy(reward);

            return Task.CompletedTask;
        }

        internal static Reward ToDetachedCopy(Reward reward)
            => Reward.FromPersistence(
                reward.Id!.Value, reward.UserId.Value, reward.Name.Value, reward.Cost, reward.Icon, reward.CreatedAt, reward.ArchivedAt);
    }
}
