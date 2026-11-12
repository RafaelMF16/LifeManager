using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Habits
{
    public class HabitReminderRepository(LifeManagerDbContext dbContext) : IHabitReminderRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<IReadOnlyList<UserId>> GetUserIdsWithRemindersAtAsync(int hour, CancellationToken cancellationToken)
        {
            // Served by the partial index on ReminderHour (active habits with a reminder).
            return await _dbContext.Habits
                .AsNoTracking()
                .Where(habit => habit.ReminderHour == hour && habit.Kind == HabitKind.Positive && habit.ArchivedAt == null)
                .Select(habit => habit.UserId)
                .Distinct()
                .Where(userId => _dbContext.PushSubscriptions.Any(subscription => subscription.UserId == userId))
                .Where(userId => _dbContext.Users.Any(user => user.Id == userId && user.DeletionScheduledFor == null))
                .OrderBy(userId => userId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlySet<HabitId>> ReserveAsync(IReadOnlyList<HabitId> habitIds, DateOnly date, CancellationToken cancellationToken)
        {
            if (habitIds.Count == 0)
                return new HashSet<HabitId>();

            var ids = habitIds.Select(habitId => habitId.Value).Distinct().ToArray();

            // One statement: the primary key (HabitId, Date) lets only one run insert each row, and RETURNING hands back
            // exactly the rows this run inserted, so a concurrent run gets the others (none) and sends nothing twice.
            var reserved = await _dbContext.Database
                .SqlQuery<int>($"""
                    INSERT INTO "HabitReminderLogs" ("HabitId", "Date")
                    SELECT id, {date} FROM unnest({ids}) AS id
                    ON CONFLICT DO NOTHING
                    RETURNING "HabitId" AS "Value"
                    """)
                .ToListAsync(cancellationToken);

            return reserved.Select(id => new HabitId(id)).ToHashSet();
        }

        public async Task<int> DeleteLogsBeforeAsync(DateOnly date, CancellationToken cancellationToken)
        {
            return await _dbContext.HabitReminderLogs
                .Where(log => log.Date < date)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
