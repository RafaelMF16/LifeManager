using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.FinanceDashboard.Errors
{
    public static class FinanceDashboardErrors
    {
        public static readonly Error InvalidFrom = Error.Validation("FinanceDashboard.InvalidFrom", "From must be a month in the yyyy-MM format");
        public static readonly Error InvalidTo = Error.Validation("FinanceDashboard.InvalidTo", "To must be a month in the yyyy-MM format");
        public static readonly Error EndBeforeStart = Error.Validation("FinanceDashboard.EndBeforeStart", "To cannot be before From");
        public static readonly Error PeriodTooLong = Error.Validation("FinanceDashboard.PeriodTooLong", "The period cannot be longer than 36 months");
        public static readonly Error InvalidComparison = Error.Validation("FinanceDashboard.InvalidComparison", "Comparison must be PreviousPeriod or SamePeriodLastYear");
        public static readonly Error ComparisonTooLong = Error.Validation("FinanceDashboard.ComparisonTooLong", "Comparing with the same period last year needs a period of at most 12 months");
    }
}
