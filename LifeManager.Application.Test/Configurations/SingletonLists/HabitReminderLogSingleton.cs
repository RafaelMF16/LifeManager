using LifeManager.Domain.Habits;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class HabitReminderLogSingleton : List<HabitReminderLog>
    {
        private HabitReminderLogSingleton() { }

        private static readonly Lazy<HabitReminderLogSingleton> lazy = new(() => new HabitReminderLogSingleton());

        public static HabitReminderLogSingleton Instance => lazy.Value;
    }
}
