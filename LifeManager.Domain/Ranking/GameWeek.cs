using LifeManager.Domain.Habits;

namespace LifeManager.Domain.Ranking
{
    /// <summary>
    /// The game's week, Monday to Sunday (the same week as <see cref="StreakCalculator.WeekStart"/>). Pure date rules
    /// shared by the ranking and the weekly challenges.
    /// </summary>
    public static class GameWeek
    {
        public const int Days = 7;

        public static DateOnly Start(DateOnly date)
            => StreakCalculator.WeekStart(date);

        public static DateOnly End(DateOnly weekStart)
            => weekStart.AddDays(Days - 1);

        public static bool IsWeekStart(DateOnly date)
            => date.DayOfWeek == DayOfWeek.Monday;

        /// <summary>
        /// Whether no day of the week can change anymore: its Sunday is past the check-in window (from Tuesday on). The
        /// day close may still be catching up, so callers that read its results check its cursor too.
        /// </summary>
        public static bool IsSettled(DateOnly weekStart, DateOnly today)
        {
            var sunday = End(weekStart);

            return sunday < today && !StreakCalculator.IsEditable(sunday, today);
        }

        /// <summary>The Monday of the latest settled week (<see cref="IsSettled"/>): the one before last until Tuesday.</summary>
        public static DateOnly LastSettledStart(DateOnly today)
            => Start(today.AddDays(-(Days + 1)));
    }
}
