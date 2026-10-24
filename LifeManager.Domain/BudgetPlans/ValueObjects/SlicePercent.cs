using LifeManager.Domain.BudgetPlans.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.BudgetPlans.ValueObjects
{
    /// <summary>A slice's share of the month's total, in percent (25 = 25%).</summary>
    public class SlicePercent
    {
        public const int MaxDecimalPlaces = 2;
        public const decimal MaxValue = 100m;

        public decimal Value { get; }

        private SlicePercent(decimal value)
        {
            Value = value;
        }

        public static Result<SlicePercent> Create(decimal value)
        {
            if (value <= 0 || value > MaxValue)
                return BudgetPlanErrors.PercentOutOfRange;

            if (decimal.Round(value, MaxDecimalPlaces) != value)
                return BudgetPlanErrors.PercentTooManyDecimals;

            return new SlicePercent(value);
        }

        internal static SlicePercent FromPersistence(decimal value) => new(value);

        public override bool Equals(object? obj)
        {
            if (obj is SlicePercent other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
