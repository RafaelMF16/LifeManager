using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Budgets.Errors
{
    public static class BudgetErrors
    {
        public static readonly Error NotFound = Error.NotFound("Budget.NotFound", "Budget not found");

        public static readonly Error InvalidType = Error.Validation("Budget.InvalidType", "Budget type must be Expense or Investment");
        public static readonly Error InvalidMonth = Error.Validation("Budget.InvalidMonth", "Month must be a month in the yyyy-MM format");
        public static readonly Error AmountNotPositive = Error.Validation("Budget.AmountNotPositive", "BudgetAmount must be greater than zero");
        public static readonly Error AmountTooManyDecimals = Error.Validation("Budget.AmountTooManyDecimals", "BudgetAmount cannot have more than 2 decimal places");
        public static readonly Error AmountTooLarge = Error.Validation("Budget.AmountTooLarge", "BudgetAmount is too large");
    }
}
