using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Habits
{
    /// <summary>One day of a habit's history.</summary>
    public record HabitDay(DateOnly Date, HabitDayState State);

    public enum HabitConsistencyUnit
    {
        Days = 1,
        Weeks = 2
    }

    /// <summary>
    /// How often the habit was kept lately: <see cref="Achieved"/> of <see cref="Due"/> days (or weeks).
    /// <see cref="Percent"/> is null while nothing was judged yet.
    /// </summary>
    public record HabitConsistency(int Achieved, int Due, int? Percent, HabitConsistencyUnit Unit);

    /// <summary>
    /// A habit's history read for its details screen: the heatmap's days and the recent consistency. Pure: works only on
    /// the check-ins it is given, so a failure is never inferred, only read from a stored day.
    /// </summary>
    public static class HabitStats
    {
        /// <summary>The days the heatmap shows, today included: 13 weeks.</summary>
        public const int HeatmapDays = 91;

        /// <summary>The window of a daily or set-days habit's consistency, today included.</summary>
        public const int ConsistencyDays = 30;

        /// <summary>The closed weeks a times-per-week habit's consistency looks at.</summary>
        public const int ConsistencyWeeks = 4;

        /// <summary>The window to load check-ins for, covering both the heatmap and the consistency.</summary>
        public static DateOnly HistoryStart(DateOnly today)
        {
            var heatmapStart = today.AddDays(-(HeatmapDays - 1));
            var weeksStart = StreakCalculator.WeekStart(today).AddDays(-7 * (ConsistencyWeeks + 1));

            return heatmapStart < weeksStart ? heatmapStart : weeksStart;
        }

        /// <summary>The last <paramref name="days"/> days, oldest first.</summary>
        public static IReadOnlyList<HabitDay> Days(Habit habit, IReadOnlyCollection<HabitCheckIn> checkIns, DateOnly today, int days = HeatmapDays)
        {
            var byDate = checkIns.ToDictionary(checkIn => checkIn.Date, checkIn => checkIn.Status);

            return [.. Enumerable.Range(0, days).Select(offset => today.AddDays(offset - (days - 1))).Select(date => new HabitDay(date, State(habit, byDate, date, today)))];
        }

        /// <summary>
        /// Daily and set-days habits: the due days of the last <see cref="ConsistencyDays"/> with a stored outcome, where
        /// done or clean count as kept and missed or relapsed as not; a day a streak freeze covered stays out, as does a
        /// day with nothing stored (archived, or not judged yet). Times-per-week habits: the last
        /// <see cref="ConsistencyWeeks"/> closed weeks, kept when the target was met (to build) or the limit respected
        /// (to avoid).
        /// </summary>
        public static HabitConsistency Consistency(Habit habit, IReadOnlyCollection<HabitCheckIn> checkIns, DateOnly today)
            => habit.FrequencyType == HabitFrequencyType.TimesPerWeek
                ? WeeklyConsistency(habit, checkIns, today)
                : DailyConsistency(habit, checkIns, today);

        /// <summary>Done or clean: the days the habit was actually kept.</summary>
        public static bool IsKept(HabitCheckInStatus status)
            => status is HabitCheckInStatus.Done or HabitCheckInStatus.Clean;

        private static HabitDayState State(Habit habit, IReadOnlyDictionary<DateOnly, HabitCheckInStatus> byDate, DateOnly date, DateOnly today)
        {
            if (date < habit.StartDate)
                return HabitDayState.BeforeStart;

            if (!habit.Frequency.IsScheduledOn(date))
                return HabitDayState.Off;

            if (byDate.TryGetValue(date, out var status))
                return status switch
                {
                    HabitCheckInStatus.Done => HabitDayState.Done,
                    HabitCheckInStatus.Clean => HabitDayState.Clean,
                    HabitCheckInStatus.Frozen => HabitDayState.Frozen,
                    HabitCheckInStatus.Missed => HabitDayState.Missed,
                    HabitCheckInStatus.Relapse => HabitDayState.Relapse,
                    _ => HabitDayState.None
                };

            // A weekly habit has no due day; an archived one isn't counted at all.
            if (habit.FrequencyType != HabitFrequencyType.TimesPerWeek && !habit.IsArchived && StreakCalculator.IsEditable(date, today))
                return HabitDayState.Pending;

            return HabitDayState.None;
        }

        private static HabitConsistency DailyConsistency(Habit habit, IReadOnlyCollection<HabitCheckIn> checkIns, DateOnly today)
        {
            var windowStart = today.AddDays(-(ConsistencyDays - 1));
            if (windowStart < habit.StartDate)
                windowStart = habit.StartDate;

            var judged = checkIns
                .Where(checkIn => checkIn.Date >= windowStart
                    && checkIn.Date <= today
                    && checkIn.Status != HabitCheckInStatus.Frozen
                    && habit.Frequency.IsScheduledOn(checkIn.Date))
                .ToList();

            return Build(judged.Count(checkIn => IsKept(checkIn.Status)), judged.Count, HabitConsistencyUnit.Days);
        }

        private static HabitConsistency WeeklyConsistency(Habit habit, IReadOnlyCollection<HabitCheckIn> checkIns, DateOnly today)
        {
            var achieved = 0;
            var due = 0;

            // The latest week whose Sunday can't be edited any more is the latest closed one.
            var weekStart = StreakCalculator.WeekStart(today);
            if (StreakCalculator.IsEditable(weekStart.AddDays(-1), today))
                weekStart = weekStart.AddDays(-7);
            weekStart = weekStart.AddDays(-7);

            // Weeks that started before the habit are never judged, like the day close does.
            for (var week = 0; week < ConsistencyWeeks && weekStart >= habit.StartDate; week++, weekStart = weekStart.AddDays(-7))
            {
                var weekEnd = weekStart.AddDays(6);
                var weekCheckIns = checkIns.Where(checkIn => checkIn.Date >= weekStart && checkIn.Date <= weekEnd).ToList();

                var kept = habit.Kind == HabitKind.Negative
                    ? weekCheckIns.Count(checkIn => checkIn.Status == HabitCheckInStatus.Relapse) <= (habit.TimesPerWeek ?? 0)
                    : weekCheckIns.Count(checkIn => checkIn.IsSuccess) >= (habit.TimesPerWeek ?? 0);

                due++;
                if (kept)
                    achieved++;
            }

            return Build(achieved, due, HabitConsistencyUnit.Weeks);
        }

        private static HabitConsistency Build(int achieved, int due, HabitConsistencyUnit unit)
            => new(achieved, due, due == 0 ? null : (int)Math.Round(100m * achieved / due, MidpointRounding.AwayFromZero), unit);
    }
}
