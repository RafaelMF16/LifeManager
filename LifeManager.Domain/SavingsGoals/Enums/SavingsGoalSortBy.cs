namespace LifeManager.Domain.SavingsGoals.Enums
{
    public enum SavingsGoalSortBy
    {
        Name = 1,

        /// <summary>Goals without a deadline come last ascending and first descending (PostgreSQL's NULL order).</summary>
        TargetMonth = 2,

        /// <summary>The share of the target already saved (initial amount plus linked investments).</summary>
        Progress = 3
    }
}
