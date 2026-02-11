using LifeManager.Domain.Exceptions;

namespace LifeManager.Domain.Transactions.ValueObjects
{
    public class TransactionDescription
    {
        public string Value { get; }

        private TransactionDescription(string value)
        {
            Value = value;
        }

        public static TransactionDescription Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException($"{nameof(TransactionDescription)} is required");

            return new TransactionDescription(value);
        }

        public override bool Equals(object? obj)
        {
            if (obj is TransactionDescription other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}