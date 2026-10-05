using LifeManager.Domain.MonthlySummaries.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.MonthlySummaries.ValueObjects
{
    public class TotalInvestment
    {
        public static readonly TotalInvestment Zero = new(0m);

        public decimal Value { get; }

        private TotalInvestment(decimal value)
        {
            Value = value;
        }

        public static Result<TotalInvestment> Create(decimal value)
        {
            // `< 0`, not decimal.IsNegative: negating an empty sum gives a negative zero (-0m), which IsNegative rejects.
            if (value < 0)
                return MonthlySummaryErrors.TotalInvestmentNegative;

            return new TotalInvestment(value);
        }

        internal static TotalInvestment FromPersistence(decimal value) => new(value);

        public override bool Equals(object? obj)
        {
            if (obj is TotalInvestment other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
