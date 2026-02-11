using LifeManager.Domain.Exceptions;

namespace LifeManager.Domain.MonthlySummaries.ValueObjects
{
    public class Balance
    {
        public decimal Value { get; }

        private Balance(decimal value)
        {
            Value = value;
        }

        public static Balance Create(TotalIncome totalIncome, TotalExpense totalExpense)
        {
            var value = totalIncome.Value - totalExpense.Value;

            return new Balance(value);
        }

        public override bool Equals(object? obj)
        {
            if (obj is Balance other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}