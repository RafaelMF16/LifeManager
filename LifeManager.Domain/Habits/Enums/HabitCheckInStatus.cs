namespace LifeManager.Domain.Habits.Enums
{
    /// <summary>How a habit's day ended. Stored as its number, so never reorder or reuse the values.</summary>
    public enum HabitCheckInStatus
    {
        /// <summary>The user checked it in.</summary>
        Done = 1,

        /// <summary>Judged by the day close: due and not done.</summary>
        Missed = 2,

        /// <summary>Missed, but a streak freeze covered it: the streak goes on and there is no damage.</summary>
        Frozen = 3,

        /// <summary>A habit to avoid that the user gave in to.</summary>
        Relapse = 4,

        /// <summary>A habit to avoid that went a whole day without a relapse.</summary>
        Clean = 5
    }
}
