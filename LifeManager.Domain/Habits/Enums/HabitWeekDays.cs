namespace LifeManager.Domain.Habits.Enums
{
    /// <summary>The days of the week a <see cref="HabitFrequencyType.WeekDays"/> habit is scheduled on, stored as a bit mask.</summary>
    [Flags]
    public enum HabitWeekDays
    {
        None = 0,
        Monday = 1,
        Tuesday = 2,
        Wednesday = 4,
        Thursday = 8,
        Friday = 16,
        Saturday = 32,
        Sunday = 64,
        All = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday
    }
}
