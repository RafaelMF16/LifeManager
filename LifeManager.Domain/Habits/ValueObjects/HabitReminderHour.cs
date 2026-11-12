using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Habits.ValueObjects
{
    /// <summary>
    /// The hour of the day (0–23, in the business time zone) a habit to build reminds the player, if still not done. The
    /// reminders job runs once an hour, so the notification arrives in the first minutes of that hour.
    /// </summary>
    public class HabitReminderHour
    {
        public const int MinValue = 0;
        public const int MaxValue = 23;

        public int Value { get; }

        private HabitReminderHour(int value)
        {
            Value = value;
        }

        /// <returns>Null (no reminder) when <paramref name="value"/> is null.</returns>
        public static Result<HabitReminderHour?> Create(int? value)
        {
            if (value is null)
                return (HabitReminderHour?)null;

            if (value is < MinValue or > MaxValue)
                return HabitErrors.InvalidReminderHour;

            return new HabitReminderHour(value.Value);
        }

        public override bool Equals(object? obj)
            => obj is HabitReminderHour other && Value == other.Value;

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
