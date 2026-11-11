using LifeManager.Domain.SavingsGoals.Enums;
using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.SavingsGoals.Interfaces
{
    public interface ISavingsGoalRepository
    {
        Task<SavingsGoal> AddAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken);

        /// <summary>Archived goals included.</summary>
        Task<SavingsGoal?> GetByIdAsync(SavingsGoalId savingsGoalId, UserId userId, CancellationToken cancellationToken);

        /// <param name="normalizedSearch">Already normalized (see <c>SearchText.Normalize</c>); null or empty means no filter.</param>
        Task<PagedList<SavingsGoal>> GetPagedByUserIdAsync(
            UserId userId,
            PageRequest pageRequest,
            SavingsGoalStatusFilter statusFilter,
            string? normalizedSearch,
            SavingsGoalSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken);

        /// <summary>
        /// Whether another active (not archived) goal of the user has this name; case- and accent-insensitive, comparing
        /// <see cref="SavingsGoalName.NormalizedValue"/>.
        /// </summary>
        Task<bool> ExistsActiveByNameAsync(UserId userId, SavingsGoalName name, SavingsGoalId? ignoredSavingsGoalId, CancellationToken cancellationToken);

        /// <summary>Saves every field the user, archiving or restoring can change (not <see cref="SavingsGoal.CompletedAt"/>).</summary>
        Task UpdateAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken);
    }
}
