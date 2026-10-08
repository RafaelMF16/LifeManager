using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Habits.ValueObjects
{
    /// <summary>What the habit is about, in the user's words. Optional: blank means none.</summary>
    public class HabitDescription
    {
        public const short MaxLength = 200;

        public string Value { get; }

        private HabitDescription(string value)
        {
            Value = value;
        }

        /// <returns>Null when <paramref name="value"/> is null or blank.</returns>
        public static Result<HabitDescription?> Create(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return (HabitDescription?)null;

            var trimmedValue = value.Trim();
            if (trimmedValue.Length > MaxLength)
                return HabitErrors.DescriptionTooLong;

            return new HabitDescription(trimmedValue);
        }

        internal static HabitDescription FromPersistence(string value) => new(value);

        public override bool Equals(object? obj)
        {
            if (obj is HabitDescription other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
