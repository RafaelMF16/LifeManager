using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Habits.Mocks
{
    /// <summary>Mirrors the real query (active habits to build of the hour, a device, no deletion pending) and the log's lock.</summary>
    public class HabitReminderRepositoryMock : IHabitReminderRepository
    {
        public Task<IReadOnlyList<UserId>> GetUserIdsWithRemindersAtAsync(int hour, CancellationToken cancellationToken)
        {
            IReadOnlyList<UserId> userIds = [.. HabitSingleton.Instance
                .Where(habit => habit.ReminderHour == hour && habit.Kind == HabitKind.Positive && !habit.IsArchived)
                .Select(habit => habit.UserId)
                .Distinct()
                .Where(userId => PushSubscriptionSingleton.Instance.Any(subscription => subscription.UserId == userId))
                .Where(userId => !UserSingleton.Instance.Any(user => user.Id == userId && user.IsDeletionPending))
                .OrderBy(userId => userId.Value)];

            return Task.FromResult(userIds);
        }

        public Task<IReadOnlySet<HabitId>> ReserveAsync(IReadOnlyList<HabitId> habitIds, DateOnly date, CancellationToken cancellationToken)
        {
            var reserved = new HashSet<HabitId>();

            foreach (var habitId in habitIds.Distinct())
            {
                if (HabitReminderLogSingleton.Instance.Any(log => log.HabitId == habitId && log.Date == date))
                    continue;

                HabitReminderLogSingleton.Instance.Add(new HabitReminderLog(habitId, date));
                reserved.Add(habitId);
            }

            return Task.FromResult<IReadOnlySet<HabitId>>(reserved);
        }

        public Task<int> DeleteLogsBeforeAsync(DateOnly date, CancellationToken cancellationToken)
            => Task.FromResult(HabitReminderLogSingleton.Instance.RemoveAll(log => log.Date < date));
    }
}
