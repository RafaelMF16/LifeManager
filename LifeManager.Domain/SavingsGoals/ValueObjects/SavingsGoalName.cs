using LifeManager.Domain.SavingsGoals.Errors;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.Text;

namespace LifeManager.Domain.SavingsGoals.ValueObjects
{
    public class SavingsGoalName
    {
        public const short MaxLength = 60;

        public string Value { get; }

        /// <summary>Case- and accent-insensitive form, used for searching and for name uniqueness.</summary>
        public string NormalizedValue { get; }

        private SavingsGoalName(string value)
        {
            Value = value;
            NormalizedValue = SearchText.Normalize(value);
        }

        public static Result<SavingsGoalName> Create(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return SavingsGoalErrors.NameIsNullOrWhiteSpace;

            var trimmedValue = value.Trim();
            if (trimmedValue.Length > MaxLength)
                return SavingsGoalErrors.NameTooLong;

            return new SavingsGoalName(trimmedValue);
        }

        internal static SavingsGoalName FromPersistence(string value) => new(value);

        public override bool Equals(object? obj)
            => obj is SavingsGoalName other && Value == other.Value;

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
