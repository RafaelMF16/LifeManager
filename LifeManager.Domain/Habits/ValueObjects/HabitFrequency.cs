using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Habits.ValueObjects
{
    /// <summary>
    /// How often a habit should be done. <see cref="WeekDays"/> is only set for <see cref="HabitFrequencyType.WeekDays"/>
    /// and <see cref="TimesPerWeek"/> only for <see cref="HabitFrequencyType.TimesPerWeek"/>.
    /// </summary>
    public class HabitFrequency
    {
        public const int MinTimesPerWeek = 1;

        /// <summary>Seven times a week is a daily habit.</summary>
        public const int MaxTimesPerWeek = 6;

        public HabitFrequencyType Type { get; }
        public HabitWeekDays WeekDays { get; }
        public int? TimesPerWeek { get; }

        private HabitFrequency(HabitFrequencyType type, HabitWeekDays weekDays, int? timesPerWeek)
        {
            Type = type;
            WeekDays = weekDays;
            TimesPerWeek = timesPerWeek;
        }

        public static HabitFrequency Daily { get; } = new(HabitFrequencyType.Daily, HabitWeekDays.None, null);

        public static Result<HabitFrequency> Create(HabitFrequencyType type, HabitWeekDays weekDays, int? timesPerWeek)
        {
            if (!Enum.IsDefined(type))
                return HabitErrors.InvalidFrequencyType;

            if ((weekDays & ~HabitWeekDays.All) != HabitWeekDays.None)
                return HabitErrors.InvalidFrequencyCombination;

            switch (type)
            {
                case HabitFrequencyType.Daily:
                    if (weekDays != HabitWeekDays.None || timesPerWeek is not null)
                        return HabitErrors.InvalidFrequencyCombination;
                    return Daily;

                case HabitFrequencyType.WeekDays:
                    if (timesPerWeek is not null)
                        return HabitErrors.InvalidFrequencyCombination;
                    if (weekDays == HabitWeekDays.None)
                        return HabitErrors.WeekDaysRequired;
                    return new HabitFrequency(type, weekDays, null);

                default:
                    if (weekDays != HabitWeekDays.None)
                        return HabitErrors.InvalidFrequencyCombination;
                    if (timesPerWeek is not (>= MinTimesPerWeek and <= MaxTimesPerWeek))
                        return HabitErrors.InvalidTimesPerWeek;
                    return new HabitFrequency(type, HabitWeekDays.None, timesPerWeek);
            }
        }

        internal static HabitFrequency FromPersistence(HabitFrequencyType type, HabitWeekDays weekDays, int? timesPerWeek)
            => new(type, weekDays, timesPerWeek);

        /// <summary>
        /// Whether <paramref name="date"/> is a day the habit must be done on. A <see cref="HabitFrequencyType.TimesPerWeek"/>
        /// habit has no fixed days: it is judged by the week, so every day counts as available.
        /// </summary>
        public bool IsScheduledOn(DateOnly date)
            => Type != HabitFrequencyType.WeekDays || WeekDays.HasFlag(ToWeekDay(date.DayOfWeek));

        public static HabitWeekDays ToWeekDay(DayOfWeek dayOfWeek)
            => dayOfWeek switch
            {
                DayOfWeek.Monday => HabitWeekDays.Monday,
                DayOfWeek.Tuesday => HabitWeekDays.Tuesday,
                DayOfWeek.Wednesday => HabitWeekDays.Wednesday,
                DayOfWeek.Thursday => HabitWeekDays.Thursday,
                DayOfWeek.Friday => HabitWeekDays.Friday,
                DayOfWeek.Saturday => HabitWeekDays.Saturday,
                DayOfWeek.Sunday => HabitWeekDays.Sunday,
                _ => throw new ArgumentOutOfRangeException(nameof(dayOfWeek), dayOfWeek, null)
            };

        public override bool Equals(object? obj)
        {
            if (obj is HabitFrequency other)
                return Type == other.Type && WeekDays == other.WeekDays && TimesPerWeek == other.TimesPerWeek;

            return false;
        }

        public override int GetHashCode()
            => HashCode.Combine(Type, WeekDays, TimesPerWeek);
    }
}
