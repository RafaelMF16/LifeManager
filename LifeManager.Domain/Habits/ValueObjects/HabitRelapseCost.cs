using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Habits.ValueObjects
{
    /// <summary>
    /// What one relapse of a habit to avoid costs in money ("a delivery: R$ 45"): each clean day is that much saved.
    /// Money rules like a transaction's amount, so it fits the same numeric(14, 2) column.
    /// </summary>
    public class HabitRelapseCost
    {
        public const int MaxDecimalPlaces = 2;
        public const decimal MaxValue = 999_999_999_999.99m;

        public decimal Value { get; }

        private HabitRelapseCost(decimal value)
        {
            Value = value;
        }

        /// <returns>Null (no cost) when <paramref name="value"/> is null.</returns>
        public static Result<HabitRelapseCost?> Create(decimal? value)
        {
            if (value is null)
                return (HabitRelapseCost?)null;

            if (value <= 0)
                return HabitErrors.RelapseCostNotPositive;

            if (decimal.Round(value.Value, MaxDecimalPlaces) != value)
                return HabitErrors.RelapseCostTooManyDecimals;

            if (value > MaxValue)
                return HabitErrors.RelapseCostTooLarge;

            return new HabitRelapseCost(value.Value);
        }

        public override bool Equals(object? obj)
            => obj is HabitRelapseCost other && Value == other.Value;

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
