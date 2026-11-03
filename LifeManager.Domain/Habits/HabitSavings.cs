namespace LifeManager.Domain.Habits
{
    /// <summary>One habit to avoid with a cost per relapse, and its clean days in a period.</summary>
    public record HabitCleanDays(int HabitId, string Name, decimal RelapseCost, int CleanDays);

    /// <param name="Amount">The habit's clean days times its cost.</param>
    public record HabitSavingsItem(int HabitId, string Name, int CleanDays, decimal Amount);

    public record HabitSavingsSummary(decimal Total, IReadOnlyList<HabitSavingsItem> Habits);

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

        /// <summary>A period's savings, the habits that saved the most first (ties by name, then id).</summary>
        public static HabitSavingsSummary ForPeriod(IEnumerable<HabitCleanDays> habits)
        {
            var items = habits
                .Select(habit => new HabitSavingsItem(habit.HabitId, habit.Name, habit.CleanDays, habit.RelapseCost * habit.CleanDays))
                .OrderByDescending(item => item.Amount)
                .ThenBy(item => item.Name, StringComparer.Ordinal)
                .ThenBy(item => item.HabitId)
                .ToList();

            return new HabitSavingsSummary(items.Sum(item => item.Amount), items);
        }
    }
}
