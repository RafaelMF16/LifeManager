using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Transactions.Errors;

namespace LifeManager.Domain.Transactions.ValueObjects
{
    public class TransactionAmount
    {
        public const int MaxDecimalPlaces = 2;

        /// <summary>Largest value that fits the persisted numeric(14, 2) column.</summary>
        public const decimal MaxValue = 999_999_999_999.99m;

        public decimal Value { get; }

        private TransactionAmount(decimal value)
        {
            Value = value;
        }

        /// <summary>Always positive: the transaction's type says whether the money comes in or goes out.</summary>
        public static Result<TransactionAmount> Create(decimal value)
        {
            if (value <= 0)
                return TransactionErrors.AmountNotPositive;

            if (decimal.Round(value, MaxDecimalPlaces) != value)
                return TransactionErrors.AmountTooManyDecimals;

            if (value > MaxValue)
                return TransactionErrors.AmountTooLarge;

            return new TransactionAmount(value);
        }

        internal static TransactionAmount FromPersistence(decimal value) => new(value);

        public override bool Equals(object? obj)
        {
            if (obj is TransactionAmount other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
