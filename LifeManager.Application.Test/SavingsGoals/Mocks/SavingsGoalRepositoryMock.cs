using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.SavingsGoals;
using LifeManager.Domain.SavingsGoals.Enums;
using LifeManager.Domain.SavingsGoals.Interfaces;
using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.SavingsGoals.Mocks
{
    public class SavingsGoalRepositoryMock : ISavingsGoalRepository
    {
        private readonly SavingsGoalSingleton _instance = SavingsGoalSingleton.Instance;

        public Task<SavingsGoal> AddAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken)
        {
            var newId = _instance.Count == 0 ? 1 : _instance.Max(storedGoal => storedGoal.Id!.Value) + 1;
            savingsGoal.AssignId(newId);

            _instance.Add(ToDetachedCopy(savingsGoal));

            return Task.FromResult(savingsGoal);
        }

        public Task<SavingsGoal?> GetByIdAsync(SavingsGoalId savingsGoalId, UserId userId, CancellationToken cancellationToken)
        {
            var storedGoal = _instance.SingleOrDefault(goal => goal.Id == savingsGoalId && goal.UserId == userId);

            return Task.FromResult(storedGoal is null ? null : ToDetachedCopy(storedGoal));
        }

        public Task<PagedList<SavingsGoal>> GetPagedByUserIdAsync(
            UserId userId,
            PageRequest pageRequest,
            SavingsGoalStatusFilter statusFilter,
            string? normalizedSearch,
            SavingsGoalSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken)
        {
            var query = _instance.Where(goal => goal.UserId == userId && statusFilter switch
            {
                SavingsGoalStatusFilter.Archived => goal.IsArchived,
                SavingsGoalStatusFilter.Completed => !goal.IsArchived && goal.CompletedAt is not null,
                _ => !goal.IsArchived && goal.CompletedAt is null
            });

            if (!string.IsNullOrEmpty(normalizedSearch))
                query = query.Where(goal => goal.NormalizedName.Contains(normalizedSearch, StringComparison.Ordinal));

            var descending = sortDirection == SortDirection.Desc;
            var ordered = sortBy switch
            {
                // PostgreSQL's NULL order: largest, so last ascending and first descending.
                SavingsGoalSortBy.TargetMonth => descending
                    ? query.OrderByDescending(goal => goal.TargetMonthStart ?? DateOnly.MaxValue)
                    : query.OrderBy(goal => goal.TargetMonthStart ?? DateOnly.MaxValue),
                _ => descending
                    ? query.OrderByDescending(goal => goal.NormalizedName, StringComparer.Ordinal)
                    : query.OrderBy(goal => goal.NormalizedName, StringComparer.Ordinal)
            };
            ordered = descending
                ? ordered.ThenByDescending(goal => goal.NormalizedName, StringComparer.Ordinal).ThenByDescending(goal => goal.Id!.Value)
                : ordered.ThenBy(goal => goal.NormalizedName, StringComparer.Ordinal).ThenBy(goal => goal.Id!.Value);

            var matching = ordered.ToList();
            IReadOnlyList<SavingsGoal> items = [.. matching.Skip(pageRequest.Skip).Take(pageRequest.PageSize).Select(ToDetachedCopy)];

            return Task.FromResult(new PagedList<SavingsGoal>(items, matching.Count, pageRequest.Page, pageRequest.PageSize));
        }

        public Task<bool> ExistsActiveByNameAsync(UserId userId, SavingsGoalName name, SavingsGoalId? ignoredSavingsGoalId, CancellationToken cancellationToken)
        {
            var exists = _instance.Any(goal =>
                goal.UserId == userId
                && !goal.IsArchived
                && goal.NormalizedName == name.NormalizedValue
                && (ignoredSavingsGoalId is null || goal.Id != ignoredSavingsGoalId));

            return Task.FromResult(exists);
        }

        public Task UpdateAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken)
        {
            var index = _instance.FindIndex(storedGoal => storedGoal.Id == savingsGoal.Id && storedGoal.UserId == savingsGoal.UserId);
            if (index >= 0)
            {
                // Like the real UpdateAsync, CompletedAt is not written here.
                var stored = _instance[index];
                _instance[index] = SavingsGoal.FromPersistence(
                    savingsGoal.Id!.Value, savingsGoal.UserId.Value, savingsGoal.Name.Value, savingsGoal.TargetAmount, savingsGoal.TargetMonthStart,
                    savingsGoal.InitialAmount, savingsGoal.Icon, stored.CreatedAt, savingsGoal.ArchivedAt, stored.CompletedAt);
            }

            return Task.CompletedTask;
        }

        internal static SavingsGoal ToDetachedCopy(SavingsGoal goal)
            => SavingsGoal.FromPersistence(
                goal.Id!.Value, goal.UserId.Value, goal.Name.Value, goal.TargetAmount, goal.TargetMonthStart, goal.InitialAmount, goal.Icon,
                goal.CreatedAt, goal.ArchivedAt, goal.CompletedAt);
    }
}
