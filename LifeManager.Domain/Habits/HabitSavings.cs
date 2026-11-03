namespace LifeManager.Domain.Habits
{
    /// <summary>
    /// The money a habit to avoid saves: each clean day is one relapse not paid for, at the habit's current
    /// <see cref="Habit.RelapseCost"/> (changing the cost recalculates the whole history). Informative only: nothing is
    /// posted and nothing tracks whether it was invested.
    /// </summary>
    public static class HabitSavings
    {
        /// <returns>Null when the habit has no cost.</returns>
        public static decimal? Saved(decimal? relapseCost, int cleanDays)
            => relapseCost * cleanDays;
    }
}
