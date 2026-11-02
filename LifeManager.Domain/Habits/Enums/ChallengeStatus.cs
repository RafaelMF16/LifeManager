namespace LifeManager.Domain.Habits.Enums
{
    /// <summary>Stored as its number, so never reorder or reuse the values.</summary>
    public enum ChallengeStatus
    {
        Active = 1,

        /// <summary>Reached its target and paid its reward; never undone.</summary>
        Completed = 2,

        /// <summary>The week ended short of the target. Costs nothing.</summary>
        Failed = 3,

        /// <summary>Its habit was archived while it was active.</summary>
        Cancelled = 4
    }
}
