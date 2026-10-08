using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;

namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>
    /// Converts between the API's list of days (<c>["Monday", "Thursday"]</c>) and the domain's <see cref="HabitWeekDays"/>
    /// bit mask.
    /// </summary>
    public static class HabitWeekDaysMapper
    {
        private static readonly DayOfWeek[] MondayFirst =
        [
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday,
            DayOfWeek.Saturday,
            DayOfWeek.Sunday
        ];

        /// <returns>
        /// <see cref="HabitWeekDays.None"/> for null or empty. An undefined day maps to a bit outside
        /// <see cref="HabitWeekDays.All"/>, so <see cref="HabitFrequency.Create"/> rejects it.
        /// </returns>
        public static HabitWeekDays ToMask(IEnumerable<DayOfWeek>? days)
        {
            var mask = HabitWeekDays.None;
            if (days is null)
                return mask;

            foreach (var day in days)
                mask |= Enum.IsDefined(day) ? HabitFrequency.ToWeekDay(day) : (HabitWeekDays)(1 << 7);

            return mask;
        }

        public static IReadOnlyList<DayOfWeek> ToDays(HabitWeekDays mask)
            => [.. MondayFirst.Where(day => mask.HasFlag(HabitFrequency.ToWeekDay(day)))];
    }
}
