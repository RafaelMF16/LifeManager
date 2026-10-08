using LifeManager.Domain.Rewards.Errors;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.Text;

namespace LifeManager.Domain.Rewards.ValueObjects
{
    public class RewardName
    {
        public const short MaxLength = 60;

        public string Value { get; }

        /// <summary>Case- and accent-insensitive form, used for searching and for name uniqueness.</summary>
        public string NormalizedValue { get; }

        private RewardName(string value)
        {
            Value = value;
            NormalizedValue = SearchText.Normalize(value);
        }

        public static Result<RewardName> Create(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return RewardErrors.NameIsNullOrWhiteSpace;

            var trimmedValue = value.Trim();
            if (trimmedValue.Length > MaxLength)
                return RewardErrors.NameTooLong;

            return new RewardName(trimmedValue);
        }

        internal static RewardName FromPersistence(string value) => new(value);

        public override bool Equals(object? obj)
        {
            if (obj is RewardName other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
