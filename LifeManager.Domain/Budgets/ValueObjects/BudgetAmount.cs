using LifeManager.Domain.Budgets.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Budgets.ValueObjects
{
    /// <summary>How much a month may spend (expense) or should set aside (investment).</summary>
    public class BudgetAmount
    {
        public const int MaxDecimalPlaces = 2;

        /// <summary>Largest value that fits the persisted numeric(14, 2) column.</summary>
        public const decimal MaxValue = 999_999_999_999.99m;

        public decimal Value { get; }

        private BudgetAmount(decimal value)
        {
            Value = value;
        }

        public static Result<BudgetAmount> Create(decimal value)
        {
            if (value <= 0)
                return BudgetErrors.AmountNotPositive;

            if (decimal.Round(value, MaxDecimalPlaces) != value)
                return BudgetErrors.AmountTooManyDecimals;

            if (value > MaxValue)
                return BudgetErrors.AmountTooLarge;

            return new BudgetAmount(value);
        }

        internal static BudgetAmount FromPersistence(decimal value) => new(value);

        public override bool Equals(object? obj)
        {
            if (obj is BudgetAmount other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
