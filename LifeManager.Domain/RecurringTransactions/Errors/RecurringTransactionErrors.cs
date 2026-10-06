using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.RecurringTransactions.Errors
{
    public static class RecurringTransactionErrors
    {
        public static readonly Error NotFound = Error.NotFound("RecurringTransaction.NotFound", "Recurring transaction not found");

        public static readonly Error InvalidDay = Error.Validation("RecurringTransaction.InvalidDay", "Day of month must be between 1 and 31");
        public static readonly Error InvalidStartMonth = Error.Validation("RecurringTransaction.InvalidStartMonth", "Start month must be a month in the yyyy-MM format");
        public static readonly Error InvalidEndMonth = Error.Validation("RecurringTransaction.InvalidEndMonth", "End month must be a month in the yyyy-MM format");
        public static readonly Error StartInPast = Error.Validation("RecurringTransaction.StartInPast", "Start month cannot be before the current month");
        public static readonly Error EndBeforeStart = Error.Validation("RecurringTransaction.EndBeforeStart", "End month cannot be before the start month");

        public static readonly Error StartLocked = Error.Conflict("RecurringTransaction.StartLocked", "Start month cannot change once the recurrence has started");
        public static readonly Error AlreadyPaused = Error.Conflict("RecurringTransaction.AlreadyPaused", "Recurring transaction is already paused");
        public static readonly Error NotPaused = Error.Conflict("RecurringTransaction.NotPaused", "Recurring transaction is not paused");
        public static readonly Error ChangedConcurrently = Error.Conflict("RecurringTransaction.ChangedConcurrently", "Recurring transaction was changed at the same time; try again");
    }
}
