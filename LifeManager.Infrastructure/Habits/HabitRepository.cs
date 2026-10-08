using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Habits
{
    public class HabitRepository(LifeManagerDbContext dbContext) : IHabitRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<Habit> AddAsync(Habit habit, CancellationToken cancellationToken)
        {
            _dbContext.Add(habit);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return habit;
        }

        public async Task<Habit?> GetByIdAsync(HabitId habitId, UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.Habits
                .AsNoTracking()
                .SingleOrDefaultAsync(habit => habit.Id == habitId && habit.UserId == userId, cancellationToken);
        }

        public async Task<PagedList<Habit>> GetPagedByUserIdAsync(
            UserId userId,
            PageRequest pageRequest,
            HabitStatusFilter statusFilter,
            string? normalizedSearch,
            HabitSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken)
        {
            var query = _dbContext.Habits
                .AsNoTracking()
                .Where(habit => habit.UserId == userId);

            query = statusFilter switch
            {
                HabitStatusFilter.Archived => query.Where(habit => habit.ArchivedAt != null),
                _ => query.Where(habit => habit.ArchivedAt == null)
            };

            if (!string.IsNullOrEmpty(normalizedSearch))
            {
                var pattern = QueryablePagingExtensions.ToContainsLikePattern(normalizedSearch);
                query = query.Where(habit => EF.Functions.Like(habit.NormalizedName, pattern, QueryablePagingExtensions.LikeEscapeCharacter));
            }

            return await Order(query, sortBy, sortDirection).ToPagedListAsync(pageRequest, cancellationToken);
        }

        public async Task<bool> ExistsActiveByNameAsync(UserId userId, HabitName name, HabitId? ignoredHabitId, CancellationToken cancellationToken)
        {
            var normalizedName = name.NormalizedValue;
            var query = _dbContext.Habits
                .Where(habit => habit.UserId == userId && habit.NormalizedName == normalizedName && habit.ArchivedAt == null);

            if (ignoredHabitId is not null)
                query = query.Where(habit => habit.Id != ignoredHabitId);

            return await query.AnyAsync(cancellationToken);
        }

        public async Task UpdateAsync(Habit habit, CancellationToken cancellationToken)
        {
            await _dbContext.Habits
                .Where(storedHabit => storedHabit.Id == habit.Id && storedHabit.UserId == habit.UserId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(storedHabit => storedHabit.Name, habit.Name)
                    .SetProperty(storedHabit => storedHabit.NormalizedName, habit.NormalizedName)
                    .SetProperty(storedHabit => storedHabit.Description, habit.Description)
                    .SetProperty(storedHabit => storedHabit.Trigger, habit.Trigger)
                    .SetProperty(storedHabit => storedHabit.Difficulty, habit.Difficulty)
                    .SetProperty(storedHabit => storedHabit.FrequencyType, habit.FrequencyType)
                    .SetProperty(storedHabit => storedHabit.WeekDays, habit.WeekDays)
                    .SetProperty(storedHabit => storedHabit.TimesPerWeek, habit.TimesPerWeek)
                    .SetProperty(storedHabit => storedHabit.ArchivedAt, habit.ArchivedAt)
                    .SetProperty(storedHabit => storedHabit.CurrentStreak, habit.CurrentStreak)
                    .SetProperty(storedHabit => storedHabit.EvaluatedUntil, habit.EvaluatedUntil), cancellationToken);
        }

        /// <summary>Sorts by the chosen column, then by name, ending with Id so pages are stable.</summary>
        private static IOrderedQueryable<Habit> Order(IQueryable<Habit> query, HabitSortBy sortBy, SortDirection sortDirection)
        {
            var descending = sortDirection == SortDirection.Desc;

            var ordered = sortBy switch
            {
                HabitSortBy.CreatedAt => descending
                    ? query.OrderByDescending(habit => habit.CreatedAt)
                    : query.OrderBy(habit => habit.CreatedAt),
                _ => descending
                    ? query.OrderByDescending(habit => habit.NormalizedName)
                    : query.OrderBy(habit => habit.NormalizedName)
            };

            return descending
                ? ordered.ThenByDescending(habit => habit.NormalizedName).ThenByDescending(habit => habit.Id)
                : ordered.ThenBy(habit => habit.NormalizedName).ThenBy(habit => habit.Id);
        }
    }
}
