using LifeManager.Domain.Habits.ValueObjects;

namespace LifeManager.Domain.Habits
{
    /// <summary>
    /// A habit's reminder already went (or is going) out on <see cref="Date"/>. The reminders job inserts it before
    /// sending and sends only what it inserted, so two runs in the same hour never notify twice. Old rows decide nothing
    /// and are deleted after <see cref="RetentionDays"/>.
    /// </summary>
    public sealed class HabitReminderLog
    {
        public const int RetentionDays = 2;

        public HabitId HabitId { get; private set; }
        public DateOnly Date { get; private set; }

        public HabitReminderLog(HabitId habitId, DateOnly date)
        {
            HabitId = habitId;
            Date = date;
        }
    }
}
