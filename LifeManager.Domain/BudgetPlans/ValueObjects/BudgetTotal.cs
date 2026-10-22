using LifeManager.Domain.BudgetPlans.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.BudgetPlans.ValueObjects
{
    /// <summary>How much a month plans to spend and invest: what its slices divide.</summary>
    public class BudgetTotal
    {
        public const int MaxDecimalPlaces = 2;

        /// <summary>Largest value that fits the persisted numeric(14, 2) column.</summary>
        public const decimal MaxValue = 999_999_999_999.99m;

        public decimal Value { get; }

        private BudgetTotal(decimal value)
        {
            Value = value;
        }

        public static Result<BudgetTotal> Create(decimal value)
        {
            if (value <= 0)
                return BudgetPlanErrors.TotalNotPositive;

            if (decimal.Round(value, MaxDecimalPlaces) != value)
                return BudgetPlanErrors.TotalTooManyDecimals;

            if (value > MaxValue)
                return BudgetPlanErrors.TotalTooLarge;

            return new BudgetTotal(value);
        }

        internal static BudgetTotal FromPersistence(decimal value) => new(value);

        public override bool Equals(object? obj)
        {
            if (obj is BudgetTotal other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
