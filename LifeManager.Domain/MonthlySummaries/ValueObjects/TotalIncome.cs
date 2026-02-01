using LifeManager.Domain.Exceptions;

namespace LifeManager.Domain.MonthlySummaries.ValueObjects
{
    public class TotalIncome
    {
        public decimal Value { get; }

        private TotalIncome(decimal value)
        {
            Value = value;
        }

        public static TotalIncome Create(decimal value)
        {
            if (decimal.IsNegative(value))
                throw new DomainException($"{nameof(TotalIncome)} cannot be negative.");

            return new TotalIncome(value);
        }

        public override bool Equals(object? obj)
        {
            if (obj is TotalIncome other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}