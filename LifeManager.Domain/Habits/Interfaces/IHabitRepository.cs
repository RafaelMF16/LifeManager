using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits.Interfaces
{
    public interface IHabitRepository
    {
        Task<Habit> AddAsync(Habit habit, CancellationToken cancellationToken);

        /// <summary>Archived habits included.</summary>
        Task<Habit?> GetByIdAsync(HabitId habitId, UserId userId, CancellationToken cancellationToken);

        /// <param name="normalizedSearch">Already normalized (see <c>SearchText.Normalize</c>); null or empty means no filter.</param>
        Task<PagedList<Habit>> GetPagedByUserIdAsync(
            UserId userId,
            PageRequest pageRequest,
            HabitStatusFilter statusFilter,
            string? normalizedSearch,
            HabitSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken);

        /// <summary>
        /// Every active habit of the user of that <paramref name="kind"/>, by name. Not paged: it feeds the day's
        /// checklist, which is bounded by how many habits one person keeps.
        /// </summary>
        Task<IReadOnlyList<Habit>> GetActiveByUserIdAsync(UserId userId, HabitKind kind, CancellationToken cancellationToken);

        /// <summary>
        /// Whether another active (not archived) habit of the user has this name; case- and accent-insensitive, comparing
        /// <see cref="HabitName.NormalizedValue"/>.
        /// </summary>
        Task<bool> ExistsActiveByNameAsync(UserId userId, HabitName name, HabitId? ignoredHabitId, CancellationToken cancellationToken);

        /// <summary>Saves every field the user, archiving or restoring can change.</summary>
        Task UpdateAsync(Habit habit, CancellationToken cancellationToken);
    }
}
