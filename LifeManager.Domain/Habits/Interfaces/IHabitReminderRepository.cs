using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits.Interfaces
{
    public interface IHabitReminderRepository
    {
        /// <summary>
        /// Users with an active habit to build whose reminder is at <paramref name="hour"/>, with at least one device and
        /// no account deletion pending. Whether each habit is still due today is decided per user afterwards.
        /// </summary>
        Task<IReadOnlyList<UserId>> GetUserIdsWithRemindersAtAsync(int hour, CancellationToken cancellationToken);

        /// <summary>
        /// Writes a <see cref="HabitReminderLog"/> for each habit on <paramref name="date"/> that has none yet, and returns
        /// those habits: only they may be reminded. Atomic, so two runs never both get the same habit.
        /// </summary>
        Task<IReadOnlySet<HabitId>> ReserveAsync(IReadOnlyList<HabitId> habitIds, DateOnly date, CancellationToken cancellationToken);

        /// <summary>Deletes the logs dated before <paramref name="date"/>; returns how many.</summary>
        Task<int> DeleteLogsBeforeAsync(DateOnly date, CancellationToken cancellationToken);
    }
}
