using LifeManager.Domain.SavingsGoals.Enums;
using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.SavingsGoals.Interfaces
{
    public interface ISavingsGoalRepository
    {
        /// <summary>Stores a new goal and assigns its id; <see cref="SavingsGoalWriteOutcome.NameTaken"/> when an active goal has the name.</summary>
        Task<SavingsGoalWriteOutcome> AddAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken);

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

        /// <summary>
        /// The linked <c>Investment</c> transactions of each goal, summed (goals without any are left out of the
        /// dictionary). <paramref name="recentFrom"/> starts the <see cref="SavingsGoalSums.Recent"/> sum.
        /// </summary>
        Task<IReadOnlyDictionary<SavingsGoalId, SavingsGoalSums>> GetLinkedSumsAsync(
            IReadOnlyCollection<SavingsGoalId> savingsGoalIds, DateOnly recentFrom, CancellationToken cancellationToken);

        /// <summary>The goal's linked investments summed per month, from <paramref name="from"/> on, oldest first.</summary>
        Task<IReadOnlyList<SavingsGoalMonthAmount>> GetMonthlyAmountsAsync(SavingsGoalId savingsGoalId, DateOnly from, CancellationToken cancellationToken);

        /// <summary>Stores the day the target was first reached; nothing changes when one is already stored or the goal is archived.</summary>
        Task MarkCompletedAsync(SavingsGoalId savingsGoalId, DateOnly date, CancellationToken cancellationToken);

        /// <summary>
        /// Saves the fields the user edits, only while the goal is active (never <see cref="SavingsGoal.ArchivedAt"/> or
        /// <see cref="SavingsGoal.CompletedAt"/>), so an edit can't bring back a goal archived meanwhile.
        /// </summary>
        Task<SavingsGoalWriteOutcome> UpdateAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken);

        /// <summary>Archives an active goal; false when it was already archived (or isn't the user's).</summary>
        Task<bool> ArchiveAsync(SavingsGoalId savingsGoalId, UserId userId, DateTimeOffset archivedAt, CancellationToken cancellationToken);

        /// <summary>Restores an archived goal; <see cref="SavingsGoalWriteOutcome.StateChanged"/> when it isn't archived anymore.</summary>
        Task<SavingsGoalWriteOutcome> RestoreAsync(SavingsGoalId savingsGoalId, UserId userId, CancellationToken cancellationToken);
    }
}
