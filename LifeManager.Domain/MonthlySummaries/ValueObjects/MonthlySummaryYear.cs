using LifeManager.Domain.MonthlySummaries.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.MonthlySummaries.ValueObjects
{
    public class MonthlySummaryYear
    {
        public int Value { get; }

        private MonthlySummaryYear(int value)
        {
            Value = value;
        }

        /// <summary>New monthly summaries can only be opened for the current (UTC) year.</summary>
        public static Result<MonthlySummaryYear> Create(int value)
        {
            if (value != DateTimeOffset.UtcNow.Year)
                return MonthlySummaryErrors.YearNotCurrent;

            return new MonthlySummaryYear(value);
        }

        /// <summary>Rehydrates without the current-year rule: stored summaries can belong to past years.</summary>
        internal static MonthlySummaryYear FromPersistence(int value) => new(value);

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
