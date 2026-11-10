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

        /// <summary>
        /// New monthly summaries can only be opened for the current year or the previous one (so December can still be
        /// opened in January). The caller passes the current year: the year of "today" in the business time zone
        /// (<c>AppClock</c>), never the UTC one.
        /// </summary>
        public static Result<MonthlySummaryYear> Create(int value, int currentYear)
        {
            if (value != currentYear && value != currentYear - 1)
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
