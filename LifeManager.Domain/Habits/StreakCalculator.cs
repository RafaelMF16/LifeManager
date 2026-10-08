using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;

namespace LifeManager.Domain.Habits
{
    /// <summary>
    /// Works out a habit's current streak from the days it succeeded (done, clean, or protected by a freeze) and the
    /// days it failed (relapses). A day, or week, that can still be checked in never breaks the streak: it just doesn't
    /// count yet. A relapse does break it, even today.
    /// </summary>
    public static class StreakCalculator
    {
        /// <summary>A day stays editable until the end of the next one: today and yesterday.</summary>
        public const int EditableDays = 2;

        private const int DaysPerWeek = 7;

        /// <summary>
        /// The habit's streak. A times-per-week habit counts weeks: one to build needs its <c>TimesPerWeek</c> check-ins,
        /// one to avoid (a weekly limit) needs the rest of the week clean (<c>7 - TimesPerWeek</c> days).
        /// </summary>
        /// <param name="failedDates">Relapses: each one breaks the streak on the spot.</param>
        public static int Current(Habit habit, IReadOnlySet<DateOnly> successDates, IReadOnlySet<DateOnly> failedDates, DateOnly today)
            => Current(habit.Frequency, habit.StartDate, successDates, today, failedDates, WeeklyTarget(habit));

        /// <returns>Days for daily and week-day habits; weeks (Monday to Sunday) for times-per-week habits.</returns>
        public static int Current(
            HabitFrequency frequency,
            DateOnly startDate,
            IReadOnlySet<DateOnly> successDates,
            DateOnly today,
            IReadOnlySet<DateOnly>? failedDates = null,
            int? weeklyTarget = null)
            => frequency.Type == HabitFrequencyType.TimesPerWeek
                ? CurrentWeeks(weeklyTarget ?? frequency.TimesPerWeek ?? 1, startDate, successDates, failedDates ?? EmptyDates, today)
                : CurrentDays(frequency, startDate, successDates, failedDates ?? EmptyDates, today);

        /// <summary>Successful days a times-per-week habit needs in a week; null for any other frequency.</summary>
        public static int? WeeklyTarget(Habit habit)
            => habit.FrequencyType != HabitFrequencyType.TimesPerWeek
                ? null
                : habit.Kind == HabitKind.Negative ? DaysPerWeek - (habit.TimesPerWeek ?? 0) : habit.TimesPerWeek;

        /// <summary>Whether <paramref name="date"/> can still be checked in (or undone) on <paramref name="today"/>.</summary>
        public static bool IsEditable(DateOnly date, DateOnly today)
            => date <= today && date > today.AddDays(-EditableDays);

        public static DateOnly WeekStart(DateOnly date)
            => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

        /// <summary>How many of <paramref name="dates"/> fall in <paramref name="date"/>'s week.</summary>
        public static int CountInWeek(IEnumerable<DateOnly> dates, DateOnly date)
        {
            var start = WeekStart(date);
            var end = start.AddDays(6);

            return dates.Count(day => day >= start && day <= end);
        }

        private static readonly IReadOnlySet<DateOnly> EmptyDates = new HashSet<DateOnly>();

        private static int CurrentDays(
            HabitFrequency frequency,
            DateOnly startDate,
            IReadOnlySet<DateOnly> successDates,
            IReadOnlySet<DateOnly> failedDates,
            DateOnly today)
        {
            var streak = 0;

            for (var day = today; day >= startDate; day = day.AddDays(-1))
            {
                if (!frequency.IsScheduledOn(day))
                    continue;

                if (failedDates.Contains(day))
                    break;

                if (successDates.Contains(day))
                    streak++;
                else if (!IsEditable(day, today))
                    break;
            }

            return streak;
        }

        private static int CurrentWeeks(
            int target,
            DateOnly startDate,
            IReadOnlySet<DateOnly> successDates,
            IReadOnlySet<DateOnly> failedDates,
            DateOnly today)
        {
            var streak = 0;

            for (var weekStart = WeekStart(today); weekStart.AddDays(6) >= startDate; weekStart = weekStart.AddDays(-7))
            {
                if (CountInWeek(successDates, weekStart) >= target)
                    streak++;
                // Too many failures: the week can't reach its target any more.
                else if (CountInWeek(failedDates, weekStart) > DaysPerWeek - target)
                    break;
                // The week isn't over, or its Sunday is still editable: it can still reach the target.
                else if (weekStart.AddDays(6) >= today.AddDays(1 - EditableDays))
                    continue;
                else
                    break;
            }

            return streak;
        }
    }
}
