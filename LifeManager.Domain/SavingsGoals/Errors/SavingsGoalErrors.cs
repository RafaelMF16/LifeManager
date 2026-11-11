using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.SavingsGoals.Errors
{
    public static class SavingsGoalErrors
    {
        public static readonly Error NotFound = Error.NotFound("SavingsGoal.NotFound", "Savings goal not found");

        public static readonly Error NameIsNullOrWhiteSpace = Error.Validation("SavingsGoal.NameIsNullOrWhiteSpace", "Savings goal name is required");
        public static readonly Error NameTooLong = Error.Validation("SavingsGoal.NameTooLong", "Savings goal name cannot be longer than 60 characters");
        public static readonly Error InvalidTargetAmount = Error.Validation("SavingsGoal.InvalidTargetAmount", "The target must be greater than zero, with at most 2 decimal places and at most 999999999999.99");
        public static readonly Error InvalidInitialAmount = Error.Validation("SavingsGoal.InvalidInitialAmount", "The amount already saved cannot be negative, and has at most 2 decimal places and at most 999999999999.99");
        public static readonly Error InvalidTargetMonth = Error.Validation("SavingsGoal.InvalidTargetMonth", "The deadline must be a month in the yyyy-MM format");
        public static readonly Error TargetMonthInPast = Error.Validation("SavingsGoal.TargetMonthInPast", "The deadline cannot be before the current month");
        public static readonly Error IconTooLong = Error.Validation("SavingsGoal.IconTooLong", "Savings goal icon cannot be longer than 30 characters");

        public static readonly Error NameAlreadyExists = Error.Conflict("SavingsGoal.NameAlreadyExists", "An active savings goal with this name already exists");
        public static readonly Error Archived = Error.Conflict("SavingsGoal.Archived", "An archived savings goal cannot be changed or receive transactions; restore it first");
        public static readonly Error AlreadyArchived = Error.Conflict("SavingsGoal.AlreadyArchived", "Savings goal is already archived");
        public static readonly Error NotArchived = Error.Conflict("SavingsGoal.NotArchived", "Savings goal is not archived");
    }
}
