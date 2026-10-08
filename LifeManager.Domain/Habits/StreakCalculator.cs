using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;

namespace LifeManager.Domain.Habits
{
    /// <summary>
    /// Works out a habit's current streak from the days it succeeded (done or protected by a freeze). A day, or week,
    /// that can still be checked in never breaks the streak: it just doesn't count yet.
    /// </summary>
    public static class StreakCalculator
    {
        /// <summary>A day stays editable until the end of the next one: today and yesterday.</summary>
        public const int EditableDays = 2;

        /// <returns>Days for daily and week-day habits; weeks (Monday to Sunday) for times-per-week habits.</returns>
        public static int Current(HabitFrequency frequency, DateOnly startDate, IReadOnlySet<DateOnly> successDates, DateOnly today)
            => frequency.Type == HabitFrequencyType.TimesPerWeek
                ? CurrentWeeks(frequency.TimesPerWeek ?? 1, startDate, successDates, today)
                : CurrentDays(frequency, startDate, successDates, today);

        /// <summary>Whether <paramref name="date"/> can still be checked in (or undone) on <paramref name="today"/>.</summary>
        public static bool IsEditable(DateOnly date, DateOnly today)
            => date <= today && date > today.AddDays(-EditableDays);

        public static DateOnly WeekStart(DateOnly date)
            => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

        /// <summary>How many of <paramref name="successDates"/> fall in <paramref name="date"/>'s week.</summary>
        public static int CountInWeek(IEnumerable<DateOnly> successDates, DateOnly date)
        {
            var start = WeekStart(date);
            var end = start.AddDays(6);

            return successDates.Count(day => day >= start && day <= end);
        }

        private static int CurrentDays(HabitFrequency frequency, DateOnly startDate, IReadOnlySet<DateOnly> successDates, DateOnly today)
        {
            var streak = 0;

            for (var day = today; day >= startDate; day = day.AddDays(-1))
            {
                if (!frequency.IsScheduledOn(day))
                    continue;

                if (successDates.Contains(day))
                    streak++;
                else if (!IsEditable(day, today))
                    break;
            }

            return streak;
        }

        private static int CurrentWeeks(int target, DateOnly startDate, IReadOnlySet<DateOnly> successDates, DateOnly today)
        {
            var streak = 0;

            for (var weekStart = WeekStart(today); weekStart.AddDays(6) >= startDate; weekStart = weekStart.AddDays(-7))
            {
                if (CountInWeek(successDates, weekStart) >= target)
                    streak++;
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
