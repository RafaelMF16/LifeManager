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
        WeekShort = 5
    }

    /// <summary>What the day close has to do with one habit on one closed day.</summary>
    /// <param name="Shortfall">For <see cref="HabitDayVerdictKind.WeekShort"/>: how many check-ins the week lacked.</param>
    public record HabitDayVerdict(HabitDayVerdictKind Kind, int Shortfall = 0)
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
        /// A habit to avoid's weekly limit is judged with its relapses, not here.
        /// </summary>
        private static HabitDayVerdict JudgeWeek(Habit habit, DateOnly date, IReadOnlyCollection<HabitCheckIn> weekCheckIns)
        {
            if (habit.Kind == HabitKind.Negative || date.DayOfWeek != DayOfWeek.Sunday)
                return SkipVerdict;

            if (StreakCalculator.WeekStart(date) < habit.StartDate)
                return SkipVerdict;

            var shortfall = (habit.TimesPerWeek ?? 0) - weekCheckIns.Count(checkIn => checkIn.IsSuccess);

            return shortfall > 0 ? new HabitDayVerdict(HabitDayVerdictKind.WeekShort, shortfall) : SettledVerdict;
        }
    }
}
