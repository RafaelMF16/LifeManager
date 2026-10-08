namespace LifeManager.Domain.Habits.Enums
{
    public enum HabitKind
    {
        /// <summary>Something to do: each time it's done earns coins and XP, and missing it costs HP.</summary>
        Positive = 1,

        /// <summary>Something to avoid: each clean day earns coins, and a relapse costs HP.</summary>
        Negative = 2
    }
}
