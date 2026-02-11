using LifeManager.Domain.Exceptions;

namespace LifeManager.Domain.MonthlySummaries.ValueObjects
{
    public class TotalExpense
    {
        public decimal Value { get; }

        private TotalExpense(decimal value)
        {
            Value = value;
        }

        public static TotalExpense Create(decimal value)
        {
            if (decimal.IsNegative(value))
                throw new DomainException($"{nameof(TotalExpense)} cannot be negative");

            return new TotalExpense(value);
        }

        public override bool Equals(object? obj)
        {
            if (obj is TotalExpense other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}