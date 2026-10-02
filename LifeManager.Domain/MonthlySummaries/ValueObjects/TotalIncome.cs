using LifeManager.Domain.MonthlySummaries.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.MonthlySummaries.ValueObjects
{
    public class TotalIncome
    {
        public static readonly TotalIncome Zero = new(0m);

        public decimal Value { get; }

        private TotalIncome(decimal value)
        {
            Value = value;
        }

        public static Result<TotalIncome> Create(decimal value)
        {
            // `< 0`, not decimal.IsNegative: a negative zero (-0m) is still zero.
            if (value < 0)
                return MonthlySummaryErrors.TotalIncomeNegative;

            return new TotalIncome(value);
        }

        internal static TotalIncome FromPersistence(decimal value) => new(value);

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
