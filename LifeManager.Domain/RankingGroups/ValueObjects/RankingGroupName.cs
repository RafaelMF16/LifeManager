using LifeManager.Domain.RankingGroups.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.RankingGroups.ValueObjects
{
    /// <summary>A group's name. It doesn't need to be unique: each group is only seen by its members.</summary>
    public class RankingGroupName
    {
        public const short MaxLength = 40;

        public string Value { get; }

        private RankingGroupName(string value)
        {
            Value = value;
        }

        public static Result<RankingGroupName> Create(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return RankingGroupErrors.NameIsNullOrWhiteSpace;

            var trimmedValue = value.Trim();
            if (trimmedValue.Length > MaxLength)
                return RankingGroupErrors.NameTooLong;

            return new RankingGroupName(trimmedValue);
        }

        internal static RankingGroupName FromPersistence(string value) => new(value);

        public override bool Equals(object? obj)
            => obj is RankingGroupName other && Value == other.Value;

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
