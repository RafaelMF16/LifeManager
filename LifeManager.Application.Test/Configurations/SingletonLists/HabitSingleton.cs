using LifeManager.Domain.Habits;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class HabitSingleton : List<Habit>
    {
        private HabitSingleton() { }

        private static readonly Lazy<HabitSingleton> lazy = new(() => new HabitSingleton());

        public static HabitSingleton Instance => lazy.Value;
    }
}
