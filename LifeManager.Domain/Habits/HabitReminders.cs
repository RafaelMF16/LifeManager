using LifeManager.Domain.Shared.Text;

namespace LifeManager.Domain.Habits
{
    /// <summary>A habit to remind now; for a habit with a daily target, how much is left today and its unit.</summary>
    public sealed record HabitReminder(Habit Habit, int? Remaining, string? Unit);

    /// <summary>
    /// Which habits a reminder goes out for at an hour: the same "due today and not done" of the Today screen
    /// (<see cref="HabitDayAgenda"/>), so the notification never disagrees with it, with that hour as their reminder.
    /// A times-per-week habit whose week target is already met is left out too.
    /// </summary>
    public static class HabitReminders
    {
        /// <param name="todayProgress">The total logged today per habit id (habits with a daily target); missing = 0.</param>
        public static IReadOnlyList<HabitReminder> Due(
            HabitDayAgenda agenda,
            int hour,
            IReadOnlyDictionary<int, int> todayProgress,
            DateOnly today)
            => [.. agenda.Today
                .Where(day => !day.Done
                    && day.Habit.ReminderHour == hour
                    && !HabitDayAgenda.IsWeekTargetMet(day.Habit, day.SuccessDates, today))
                .Select(day => ToReminder(day.Habit, todayProgress))
                .OrderBy(reminder => SearchText.Normalize(reminder.Habit.Name.Value), StringComparer.Ordinal)
                .ThenBy(reminder => reminder.Habit.Id!.Value)];

        private static HabitReminder ToReminder(Habit habit, IReadOnlyDictionary<int, int> todayProgress)
        {
            if (habit.Target is not { } target)
                return new HabitReminder(habit, null, null);

            var logged = todayProgress.GetValueOrDefault(habit.Id!.Value);
            return new HabitReminder(habit, Math.Max(target.Value - logged, 0), target.Unit);
        }
    }
}
