using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Habits
{
    /// <summary>A habit to build that can be checked in on <see cref="Date"/> (today, or yesterday still pending).</summary>
    /// <param name="SuccessDates">The habit's successful days among the check-ins given to the agenda.</param>
    public sealed record HabitDueDay(Habit Habit, DateOnly Date, IReadOnlySet<DateOnly> SuccessDates, bool Done);

    /// <summary>A habit to avoid in force today, with its relapse days among the check-ins given to the agenda.</summary>
    public sealed record HabitAvoidDay(Habit Habit, IReadOnlySet<DateOnly> RelapseDates, bool RelapsedToday);

    /// <summary>
    /// What the day asks of the player: the rules behind the "Today" screen, shared with the Home so both always
    /// count the same. Pure: it only reads the habits and check-ins it is given.
    /// </summary>
    /// <param name="Today">Habits to build that can be checked in today.</param>
    /// <param name="YesterdayPending">Habits to build that can still be checked in for yesterday and weren't (a weekly one only while its week's target isn't met).</param>
    /// <param name="Avoiding">Habits to avoid in force today.</param>
    /// <param name="FreeToday">Habits to avoid on a day off (set days).</param>
    public sealed record HabitDayAgenda(
        IReadOnlyList<HabitDueDay> Today,
        IReadOnlyList<HabitDueDay> YesterdayPending,
        IReadOnlyList<HabitAvoidDay> Avoiding,
        IReadOnlyList<Habit> FreeToday)
    {
        /// <param name="habitsToBuild">The user's active habits to build.</param>
        /// <param name="habitsToAvoid">The user's active habits to avoid.</param>
        /// <param name="checkIns">The user's check-ins from at least the start of yesterday's week through today.</param>
        public static HabitDayAgenda For(
            IEnumerable<Habit> habitsToBuild,
            IEnumerable<Habit> habitsToAvoid,
            IEnumerable<HabitCheckIn> checkIns,
            DateOnly today)
        {
            var yesterday = today.AddDays(-1);
            var checkInList = checkIns as IReadOnlyCollection<HabitCheckIn> ?? [.. checkIns];
            var successDatesByHabit = DatesByHabit(checkInList.Where(checkIn => checkIn.IsSuccess));
            var relapseDatesByHabit = DatesByHabit(checkInList.Where(checkIn => checkIn.Status == HabitCheckInStatus.Relapse));

            var todayItems = new List<HabitDueDay>();
            var yesterdayPending = new List<HabitDueDay>();

            foreach (var habit in habitsToBuild)
            {
                var successDates = successDatesByHabit.GetValueOrDefault(habit.Id!.Value) ?? NoDates;

                if (habit.EnsureCanCheckIn(today, today).IsSuccess)
                    todayItems.Add(new HabitDueDay(habit, today, successDates, successDates.Contains(today)));

                if (habit.EnsureCanCheckIn(yesterday, today).IsSuccess
                    && !successDates.Contains(yesterday)
                    && !IsWeekTargetMet(habit, successDates, yesterday))
                    yesterdayPending.Add(new HabitDueDay(habit, yesterday, successDates, false));
            }

            var avoiding = new List<HabitAvoidDay>();
            var freeToday = new List<Habit>();

            foreach (var habit in habitsToAvoid)
            {
                if (habit.EnsureCanRelapse(today, today).IsSuccess)
                {
                    var relapseDates = relapseDatesByHabit.GetValueOrDefault(habit.Id!.Value) ?? NoDates;
                    avoiding.Add(new HabitAvoidDay(habit, relapseDates, relapseDates.Contains(today)));
                }
                else if (habit.StartDate <= today && !habit.Frequency.IsScheduledOn(today))
                    freeToday.Add(habit);
            }

            return new HabitDayAgenda(todayItems, yesterdayPending, avoiding, freeToday);
        }

        /// <summary>Whether a times-per-week habit already met its target in the week of <paramref name="date"/>.</summary>
        public static bool IsWeekTargetMet(Habit habit, IReadOnlySet<DateOnly> successDates, DateOnly date)
            => habit.FrequencyType == HabitFrequencyType.TimesPerWeek
                && StreakCalculator.CountInWeek(successDates, date) >= habit.TimesPerWeek;

        private static readonly IReadOnlySet<DateOnly> NoDates = new HashSet<DateOnly>();

        private static Dictionary<int, IReadOnlySet<DateOnly>> DatesByHabit(IEnumerable<HabitCheckIn> checkIns)
            => checkIns
                .GroupBy(checkIn => checkIn.HabitId.Value)
                .ToDictionary(group => group.Key, group => (IReadOnlySet<DateOnly>)group.Select(checkIn => checkIn.Date).ToHashSet());
    }
}
