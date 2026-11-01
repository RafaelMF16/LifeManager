namespace LifeManager.Domain.Habits.Enums
{
    /// <summary>What a weekly challenge counts. Stored as its number, so never reorder or reuse the values.</summary>
    public enum ChallengeType
    {
        /// <summary>Days done in the week ("5 times this week").</summary>
        Frequency = 1,

        /// <summary>Progress logged in the week, for a habit with a daily target ("10 000 ml this week").</summary>
        Volume = 2
    }
}
