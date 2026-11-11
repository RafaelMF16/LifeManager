namespace LifeManager.Domain.SavingsGoals.Enums
{
    public enum SavingsGoalPace
    {
        /// <summary>The average saved over the recent months covers what each month still needs.</summary>
        OnTrack,

        /// <summary>It doesn't (or the deadline passed with money left).</summary>
        Behind,

        /// <summary>No deadline, so no pace to keep.</summary>
        NoDeadline,

        /// <summary>The target is reached.</summary>
        Done
    }
}
