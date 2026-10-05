using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.MonthlySummaries.Errors
{
    public static class MonthlySummaryErrors
    {
        public static readonly Error NotFound = Error.NotFound("MonthlySummary.NotFound", "Monthly summary not found");

        public static readonly Error InvalidMonth = Error.Validation("MonthlySummary.InvalidMonth", "Month must be between 1 and 12");
        public static readonly Error YearNotCurrent = Error.Validation("MonthlySummary.YearNotCurrent", "Monthly summary can only be created for the current year");
        public static readonly Error TotalIncomeNegative = Error.Validation("MonthlySummary.TotalIncomeNegative", "TotalIncome cannot be negative");
        public static readonly Error TotalExpenseNegative = Error.Validation("MonthlySummary.TotalExpenseNegative", "TotalExpense cannot be negative");
        public static readonly Error TotalInvestmentNegative = Error.Validation("MonthlySummary.TotalInvestmentNegative", "TotalInvestment cannot be negative");

        public static readonly Error AlreadyExists = Error.Conflict("MonthlySummary.AlreadyExists", "Monthly summary already exists for this month");
    }
}
