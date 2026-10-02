using LifeManager.Domain.MonthlySummaries.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.MonthlySummaries.ValueObjects
{
    public class TotalExpense
    {
        public static readonly TotalExpense Zero = new(0m);

        public decimal Value { get; }

        private TotalExpense(decimal value)
        {
            Value = value;
        }

        public static Result<TotalExpense> Create(decimal value)
        {
            // `< 0`, not decimal.IsNegative: negating an empty sum gives a negative zero (-0m), which IsNegative rejects.
            if (value < 0)
                return MonthlySummaryErrors.TotalExpenseNegative;

            return new TotalExpense(value);
        }

        internal static TotalExpense FromPersistence(decimal value) => new(value);

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
