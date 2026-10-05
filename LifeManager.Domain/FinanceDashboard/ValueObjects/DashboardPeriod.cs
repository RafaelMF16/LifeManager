using LifeManager.Domain.FinanceDashboard.Enums;
using LifeManager.Domain.FinanceDashboard.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.FinanceDashboard.ValueObjects
{
    /// <summary>A run of whole months, from <see cref="From"/> to <see cref="To"/> (both included).</summary>
    public sealed class DashboardPeriod
    {
        public const int MaxMonths = 36;

        /// <summary>Longest period that can be compared with the same months a year earlier without overlapping them.</summary>
        public const int MaxMonthsForSamePeriodLastYear = 12;

        public YearMonth From { get; }
        public YearMonth To { get; }

        private DashboardPeriod(YearMonth from, YearMonth to)
        {
            From = from;
            To = to;
        }

        public int MonthCount => To.Ordinal - From.Ordinal + 1;
        public DateOnly FirstDay => From.FirstDay;
        public DateOnly LastDay => To.LastDay;

        /// <param name="from">First month, "yyyy-MM".</param>
        /// <param name="to">Last month, "yyyy-MM".</param>
        public static Result<DashboardPeriod> Create(string? from, string? to)
        {
            if (!YearMonth.TryParse(from, out var fromMonth))
                return FinanceDashboardErrors.InvalidFrom;

            if (!YearMonth.TryParse(to, out var toMonth))
                return FinanceDashboardErrors.InvalidTo;

            if (toMonth.Ordinal < fromMonth.Ordinal)
                return FinanceDashboardErrors.EndBeforeStart;

            if (toMonth.Ordinal - fromMonth.Ordinal + 1 > MaxMonths)
                return FinanceDashboardErrors.PeriodTooLong;

            return new DashboardPeriod(fromMonth, toMonth);
        }

        /// <summary>Every month of the period, in order.</summary>
        public IReadOnlyList<YearMonth> Months()
            => [.. Enumerable.Range(0, MonthCount).Select(From.AddMonths)];

        public bool Contains(int year, int month)
        {
            var ordinal = year * 12 + month - 1;
            return ordinal >= From.Ordinal && ordinal <= To.Ordinal;
        }

        /// <summary>The earlier period this one is compared against; it always ends before this one starts.</summary>
        public Result<DashboardPeriod> ComparisonFor(DashboardComparison comparison)
        {
            return comparison switch
            {
                DashboardComparison.PreviousPeriod => new DashboardPeriod(From.AddMonths(-MonthCount), From.AddMonths(-1)),
                DashboardComparison.SamePeriodLastYear => MonthCount > MaxMonthsForSamePeriodLastYear
                    ? FinanceDashboardErrors.ComparisonTooLong
                    : new DashboardPeriod(From.AddMonths(-12), To.AddMonths(-12)),
                _ => FinanceDashboardErrors.InvalidComparison
            };
        }
    }
}
