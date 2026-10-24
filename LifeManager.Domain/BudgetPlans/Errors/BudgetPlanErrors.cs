using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.BudgetPlans.Errors
{
    public static class BudgetPlanErrors
    {
        public static readonly Error NotFound = Error.NotFound("BudgetPlan.NotFound", "Budget plan not found");

        public static readonly Error InvalidMonth = Error.Validation("BudgetPlan.InvalidMonth", "Month must be a month in the yyyy-MM format");
        public static readonly Error InvalidType = Error.Validation("BudgetPlan.InvalidType", "Slice type must be Expense or Investment");
        public static readonly Error TotalNotPositive = Error.Validation("BudgetPlan.TotalNotPositive", "Total must be greater than zero");
        public static readonly Error TotalTooManyDecimals = Error.Validation("BudgetPlan.TotalTooManyDecimals", "Total cannot have more than 2 decimal places");
        public static readonly Error TotalTooLarge = Error.Validation("BudgetPlan.TotalTooLarge", "Total is too large");
        public static readonly Error PercentOutOfRange = Error.Validation("BudgetPlan.PercentOutOfRange", "Percent must be greater than 0 and at most 100");
        public static readonly Error PercentTooManyDecimals = Error.Validation("BudgetPlan.PercentTooManyDecimals", "Percent cannot have more than 2 decimal places");
        public static readonly Error OverAllocated = Error.Validation("BudgetPlan.OverAllocated", "Slices cannot add up to more than 100%");
        public static readonly Error DuplicateCategory = Error.Validation("BudgetPlan.DuplicateCategory", "A category can only have one slice");
        public static readonly Error ExpenseSliceWithoutCategory = Error.Validation("BudgetPlan.ExpenseSliceWithoutCategory", "An expense slice needs a category");
        public static readonly Error InvalidInvestmentSlice = Error.Validation("BudgetPlan.InvalidInvestmentSlice", "There can be one investment slice, without a category");
    }
}
