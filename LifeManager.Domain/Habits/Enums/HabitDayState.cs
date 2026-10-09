namespace LifeManager.Domain.Habits.Enums
{
    /// <summary>How one day of a habit's history reads in its heatmap. Sent by name; never persisted.</summary>
    public enum HabitDayState
    {
        /// <summary>Before the habit's start date.</summary>
        BeforeStart = 1,

        /// <summary>A set-days habit's day off: not due (to build) or free (to avoid).</summary>
        Off = 2,

        /// <summary>Nothing recorded: a times-per-week habit's day without a check-in, an archived stretch, or a day the day close hasn't judged yet.</summary>
        None = 3,

        /// <summary>Today or yesterday, due and not recorded yet: it can still be checked in (or stay clean).</summary>
        Pending = 4,

        Done = 5,
        Clean = 6,
        Frozen = 7,
        Missed = 8,
        Relapse = 9
    }
}
