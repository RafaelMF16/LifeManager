using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.Text;

namespace LifeManager.Domain.Habits.ValueObjects
{
    public class HabitName
    {
        public const short MaxLength = 60;

        public string Value { get; }

        /// <summary>Case- and accent-insensitive form, used for searching and for name uniqueness.</summary>
        public string NormalizedValue { get; }

        private HabitName(string value)
        {
            Value = value;
            NormalizedValue = SearchText.Normalize(value);
        }

        public static Result<HabitName> Create(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return HabitErrors.NameIsNullOrWhiteSpace;

            var trimmedValue = value.Trim();
            if (trimmedValue.Length > MaxLength)
                return HabitErrors.NameTooLong;

            return new HabitName(trimmedValue);
        }

        internal static HabitName FromPersistence(string value) => new(value);

        public override bool Equals(object? obj)
        {
            if (obj is HabitName other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
