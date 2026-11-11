namespace LifeManager.Domain.SavingsGoals.Enums
{
    /// <summary>What a conditional write of a goal did.</summary>
    public enum SavingsGoalWriteOutcome
    {
        Saved,

        /// <summary>The stored goal is no longer in the state the write expects (archived or restored meanwhile, or gone).</summary>
        StateChanged,

        /// <summary>Another active goal of the user took the name meanwhile (the partial unique index refused it).</summary>
        NameTaken
    }
}
