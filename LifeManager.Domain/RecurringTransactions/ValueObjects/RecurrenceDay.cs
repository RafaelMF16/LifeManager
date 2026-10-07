using LifeManager.Domain.RecurringTransactions.Errors;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;

namespace LifeManager.Domain.RecurringTransactions.ValueObjects
{
    /// <summary>The day of the month a recurring transaction falls on.</summary>
    public class RecurrenceDay
    {
        public const int FirstDay = 1;
        public const int LastDay = 31;

        public int Value { get; }

        private RecurrenceDay(int value)
        {
            Value = value;
        }

        public static Result<RecurrenceDay> Create(int value)
        {
            if (value is < FirstDay or > LastDay)
                return RecurringTransactionErrors.InvalidDay;

            return new RecurrenceDay(value);
        }

        internal static RecurrenceDay FromPersistence(int value) => new(value);

        /// <summary>The date in <paramref name="month"/>; months shorter than the day fall on their last day (31 → Feb 28/29).</summary>
        public DateOnly DateIn(YearMonth month)
            => new(month.Year, month.Month, Math.Min(Value, DateTime.DaysInMonth(month.Year, month.Month)));

        public override bool Equals(object? obj)
        {
            if (obj is RecurrenceDay other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
