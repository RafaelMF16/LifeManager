using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Habits
{
    public enum HabitDayVerdictKind
    {
        /// <summary>Nothing to judge: not a due day, before the habit started, or a weekly habit's mid-week day.</summary>
        Skip = 1,

        /// <summary>Already settled by a check-in (done, a relapse...).</summary>
        Settled = 2,

        /// <summary>A habit to build, due and not done: damage, or a streak freeze.</summary>
        Missed = 3,

        /// <summary>A habit to avoid, due and with no relapse: rewarded like a check-in.</summary>
        Clean = 4,

        /// <summary>A times-per-week habit to build whose week closed short of its target.</summary>
        WeekShort = 5,

        /// <summary>A habit to avoid's week that closed within its weekly limit: each day without a relapse is clean.</summary>
        WeekClean = 6
    }

    /// <summary>What the day close has to do with one habit on one closed day.</summary>
    /// <param name="Shortfall">For <see cref="HabitDayVerdictKind.WeekShort"/>: how many check-ins the week lacked.</param>
    /// <param name="CleanDays">For <see cref="HabitDayVerdictKind.WeekClean"/>: the week's days without a relapse.</param>
    public record HabitDayVerdict(HabitDayVerdictKind Kind, int Shortfall = 0, IReadOnlyList<DateOnly>? CleanDays = null)
    {
        private static readonly HabitDayVerdict SkipVerdict = new(HabitDayVerdictKind.Skip);
        private static readonly HabitDayVerdict SettledVerdict = new(HabitDayVerdictKind.Settled);

        /// <param name="weekCheckIns">The habit's check-ins in <paramref name="date"/>'s week (Monday to Sunday).</param>
        public static HabitDayVerdict Judge(Habit habit, DateOnly date, IReadOnlyCollection<HabitCheckIn> weekCheckIns)
        {
            if (date < habit.StartDate)
                return SkipVerdict;

            if (habit.FrequencyType == HabitFrequencyType.TimesPerWeek)
                return JudgeWeek(habit, date, weekCheckIns);

            if (!habit.Frequency.IsScheduledOn(date))
                return SkipVerdict;

            if (weekCheckIns.Any(checkIn => checkIn.Date == date))
                return SettledVerdict;

            return new HabitDayVerdict(habit.Kind == HabitKind.Positive ? HabitDayVerdictKind.Missed : HabitDayVerdictKind.Clean);
        }

        /// <summary>
        /// A times-per-week habit is judged once, on its week's Sunday, and only for weeks that started with the habit.
        /// One to build is short by the check-ins it lacked. One to avoid (a weekly limit) is clean on every relapse-free
        /// day when it stayed within the limit; over it, each relapse past the limit already cost HP when logged.
        /// </summary>
        private static HabitDayVerdict JudgeWeek(Habit habit, DateOnly date, IReadOnlyCollection<HabitCheckIn> weekCheckIns)
        {
            if (date.DayOfWeek != DayOfWeek.Sunday)
                return SkipVerdict;

            var weekStart = StreakCalculator.WeekStart(date);
            if (weekStart < habit.StartDate)
                return SkipVerdict;

            if (habit.Kind == HabitKind.Negative)
            {
                var relapseDays = weekCheckIns.Where(checkIn => checkIn.Status == HabitCheckInStatus.Relapse).Select(checkIn => checkIn.Date).ToHashSet();
                if (relapseDays.Count > (habit.TimesPerWeek ?? 0))
                    return SettledVerdict;

                var taken = weekCheckIns.Select(checkIn => checkIn.Date).ToHashSet();
                List<DateOnly> cleanDays = [.. Enumerable.Range(0, 7).Select(weekStart.AddDays).Where(day => !taken.Contains(day))];

                return cleanDays.Count > 0 ? new HabitDayVerdict(HabitDayVerdictKind.WeekClean, CleanDays: cleanDays) : SettledVerdict;
            }

            var shortfall = (habit.TimesPerWeek ?? 0) - weekCheckIns.Count(checkIn => checkIn.IsSuccess);

            return shortfall > 0 ? new HabitDayVerdict(HabitDayVerdictKind.WeekShort, shortfall) : SettledVerdict;
        }
    }
}
