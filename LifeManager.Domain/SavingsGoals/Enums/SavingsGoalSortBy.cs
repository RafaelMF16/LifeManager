namespace LifeManager.Domain.SavingsGoals.Enums
{
    public enum SavingsGoalSortBy
    {
        Name = 1,

        /// <summary>Goals without a deadline come last ascending and first descending (PostgreSQL's NULL order).</summary>
        TargetMonth = 2
    }
}
