namespace LifeManager.Domain.MonthlySummaries.ValueObjects
{
    public class Balance
    {
        public decimal Value { get; }

        private Balance(decimal value)
        {
            Value = value;
        }

        /// <summary>What is left in the account: income minus what was spent and what was set aside as investment.</summary>
        public static Balance Create(TotalIncome totalIncome, TotalExpense totalExpense, TotalInvestment totalInvestment)
        {
            var value = totalIncome.Value - totalExpense.Value - totalInvestment.Value;

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
