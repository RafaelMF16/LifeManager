using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Habits.ValueObjects
{
    /// <summary>The cue the habit is tied to (an implementation intention: "after X, I do Y"). Optional: blank means none.</summary>
    public class HabitTrigger
    {
        public const short MaxLength = 120;

        public string Value { get; }

        private HabitTrigger(string value)
        {
            Value = value;
        }

        /// <returns>Null when <paramref name="value"/> is null or blank.</returns>
        public static Result<HabitTrigger?> Create(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return (HabitTrigger?)null;

            var trimmedValue = value.Trim();
            if (trimmedValue.Length > MaxLength)
                return HabitErrors.TriggerTooLong;

            return new HabitTrigger(trimmedValue);
        }

        internal static HabitTrigger FromPersistence(string value) => new(value);

        public override bool Equals(object? obj)
        {
            if (obj is HabitTrigger other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
