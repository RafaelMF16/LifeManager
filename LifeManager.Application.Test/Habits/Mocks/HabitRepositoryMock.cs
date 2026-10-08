using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Habits.Mocks
{
    public class HabitRepositoryMock : IHabitRepository
    {
        private readonly HabitSingleton _instance;

        public int ExistsActiveByNameCallCount { get; private set; }
        public int UpdateCallCount { get; private set; }

        public HabitRepositoryMock()
        {
            _instance = HabitSingleton.Instance;
        }

        public Task<Habit> AddAsync(Habit habit, CancellationToken cancellationToken)
        {
            var newId = _instance.Count == 0 ? 1 : _instance.Max(storedHabit => storedHabit.Id!.Value) + 1;
            habit.AssignId(newId);

            _instance.Add(ToDetachedCopy(habit));

            return Task.FromResult(habit);
        }

        public Task<Habit?> GetByIdAsync(HabitId habitId, UserId userId, CancellationToken cancellationToken)
        {
            var storedHabit = _instance.SingleOrDefault(habit => habit.Id == habitId && habit.UserId == userId);

            return Task.FromResult(storedHabit is null ? null : ToDetachedCopy(storedHabit));
        }

        public Task<PagedList<Habit>> GetPagedByUserIdAsync(
            UserId userId,
            PageRequest pageRequest,
            HabitStatusFilter statusFilter,
            string? normalizedSearch,
            HabitSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken)
        {
            var archived = statusFilter == HabitStatusFilter.Archived;
            var query = _instance.Where(habit => habit.UserId == userId && habit.IsArchived == archived);

            if (!string.IsNullOrEmpty(normalizedSearch))
                query = query.Where(habit => habit.NormalizedName.Contains(normalizedSearch, StringComparison.Ordinal));

            var descending = sortDirection == SortDirection.Desc;
            var ordered = sortBy switch
            {
                HabitSortBy.CreatedAt => descending
                    ? query.OrderByDescending(habit => habit.CreatedAt)
                    : query.OrderBy(habit => habit.CreatedAt),
                _ => descending
                    ? query.OrderByDescending(habit => habit.NormalizedName, StringComparer.Ordinal)
                    : query.OrderBy(habit => habit.NormalizedName, StringComparer.Ordinal)
            };
            ordered = descending
                ? ordered.ThenByDescending(habit => habit.NormalizedName, StringComparer.Ordinal).ThenByDescending(habit => habit.Id!.Value)
                : ordered.ThenBy(habit => habit.NormalizedName, StringComparer.Ordinal).ThenBy(habit => habit.Id!.Value);

            var matching = ordered.ToList();
            IReadOnlyList<Habit> items = [.. matching.Skip(pageRequest.Skip).Take(pageRequest.PageSize).Select(ToDetachedCopy)];

            return Task.FromResult(new PagedList<Habit>(items, matching.Count, pageRequest.Page, pageRequest.PageSize));
        }

        public Task<IReadOnlyList<Habit>> GetActiveByUserIdAsync(UserId userId, HabitKind kind, CancellationToken cancellationToken)
        {
            IReadOnlyList<Habit> habits =
            [
                .. _instance
                    .Where(habit => habit.UserId == userId && habit.Kind == kind && !habit.IsArchived)
                    .OrderBy(habit => habit.NormalizedName, StringComparer.Ordinal)
                    .ThenBy(habit => habit.Id!.Value)
                    .Select(ToDetachedCopy)
            ];

            return Task.FromResult(habits);
        }

        public Task<bool> ExistsActiveByNameAsync(UserId userId, HabitName name, HabitId? ignoredHabitId, CancellationToken cancellationToken)
        {
            ExistsActiveByNameCallCount++;

            var exists = _instance.Any(habit =>
                habit.UserId == userId
                && !habit.IsArchived
                && habit.NormalizedName == name.NormalizedValue
                && (ignoredHabitId is null || habit.Id != ignoredHabitId));

            return Task.FromResult(exists);
        }

        public Task UpdateAsync(Habit habit, CancellationToken cancellationToken)
        {
            UpdateCallCount++;

            var index = _instance.FindIndex(storedHabit => storedHabit.Id == habit.Id && storedHabit.UserId == habit.UserId);
            if (index >= 0)
                _instance[index] = ToDetachedCopy(habit);

            return Task.CompletedTask;
        }

        internal static Habit ToDetachedCopy(Habit habit)
            => Habit.FromPersistence(
                habit.Id!.Value,
                habit.UserId.Value,
                habit.Name.Value,
                habit.Description?.Value,
                habit.Trigger?.Value,
                habit.Kind,
                habit.Difficulty,
                habit.FrequencyType,
                habit.WeekDays,
                habit.TimesPerWeek,
                habit.StartDate,
                habit.CreatedAt,
                habit.ArchivedAt,
                habit.CurrentStreak,
                habit.LongestStreak,
                habit.EvaluatedUntil);
    }
}
