using LifeManager.Domain.Exceptions;

namespace LifeManager.Domain.MonthlySummaries.ValueObjects
{
    public class MonthlySummaryYear
    {
        public int Value { get; }

        private MonthlySummaryYear(int value)
        {
            Value = value;
        }

        public static MonthlySummaryYear Create(int value)
        {
            var currentYear = DateTimeOffset.UtcNow.Year;
            if (value != currentYear)
                throw new DomainException($"{nameof(MonthlySummaryYear)} can only be created for the current year.");

            return new MonthlySummaryYear(value);
        }

        public override bool Equals(object? obj)
        {
            if (obj is MonthlySummaryYear other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
