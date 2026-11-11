namespace LifeManager.Domain.SavingsGoals.Enums
{
    public enum SavingsGoalStatusFilter
    {
        /// <summary>Not archived and not completed yet.</summary>
        Active = 1,

        /// <summary>Not archived, with the target reached at some point (<see cref="SavingsGoal.CompletedAt"/>).</summary>
        Completed = 2,

        Archived = 3
    }
}
