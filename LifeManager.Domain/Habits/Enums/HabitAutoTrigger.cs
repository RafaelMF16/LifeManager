namespace LifeManager.Domain.Habits.Enums
{
    /// <summary>
    /// The finance action that checks a habit to build in by itself, today. Stored as a number; only a one-tap habit to
    /// build takes one (see <see cref="Habit.AutoTrigger"/>).
    /// </summary>
    public enum HabitAutoTrigger
    {
        None = 0,

        /// <summary>A transaction typed by hand (recurrences, imports and reward expenses don't count).</summary>
        TransactionCreated = 1,

        /// <summary>A bank statement import posted.</summary>
        StatementImportCommitted = 2,

        /// <summary>A month's budget plan saved.</summary>
        BudgetPlanSaved = 3
    }
}
