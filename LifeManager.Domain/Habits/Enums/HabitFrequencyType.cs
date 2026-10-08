namespace LifeManager.Domain.Habits.Enums
{
    public enum HabitFrequencyType
    {
        /// <summary>Every day.</summary>
        Daily = 1,

        /// <summary>On fixed days of the week (<see cref="HabitWeekDays"/>).</summary>
        WeekDays = 2,

        /// <summary>A number of times per week (Monday to Sunday), on any days.</summary>
        TimesPerWeek = 3
    }
}
