using LifeManager.Domain.Exceptions;

namespace LifeManager.Domain.MonthlySummaries.ValueObjects
{
    public class MonthlySummaryMonth
    {
        public int Value { get; }

        private MonthlySummaryMonth(int value)
        {
            Value = value;
        }

        public static MonthlySummaryMonth Create(int value)
        {
            const short firstMonth = 1;
            const short lastMonth = 12;
            if (value is < firstMonth or > lastMonth)
                throw new DomainException($"{nameof(MonthlySummaryMonth)} {value} is invalid month.");

            return new MonthlySummaryMonth(value);
        }

        public override bool Equals(object? obj)
        {
            if (obj is MonthlySummaryMonth other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}