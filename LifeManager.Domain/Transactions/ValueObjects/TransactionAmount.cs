using LifeManager.Domain.Exceptions;

namespace LifeManager.Domain.Transactions.ValueObjects
{
    public class TransactionAmount
    {
        public decimal Value { get; }

        private TransactionAmount(decimal value)
        {
            Value = value;
        }

        public static TransactionAmount Create(decimal value)
        {
            if (decimal.IsNegative(value))
                throw new DomainException($"{nameof(TransactionAmount)} cannot be negative");

            return new TransactionAmount(value);
        }

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