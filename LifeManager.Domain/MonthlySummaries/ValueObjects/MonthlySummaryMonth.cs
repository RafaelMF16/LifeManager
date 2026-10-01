using LifeManager.Domain.MonthlySummaries.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.MonthlySummaries.ValueObjects
{
    public class MonthlySummaryMonth
    {
        public const int FirstMonth = 1;
        public const int LastMonth = 12;

        public int Value { get; }

        private MonthlySummaryMonth(int value)
        {
            Value = value;
        }

        public static Result<MonthlySummaryMonth> Create(int value)
        {
            if (value is < FirstMonth or > LastMonth)
                return MonthlySummaryErrors.InvalidMonth;

            return new MonthlySummaryMonth(value);
        }

        internal static MonthlySummaryMonth FromPersistence(int value) => new(value);

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
