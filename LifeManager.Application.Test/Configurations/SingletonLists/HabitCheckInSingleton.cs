using LifeManager.Domain.Habits;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class HabitCheckInSingleton : List<HabitCheckIn>
    {
        private HabitCheckInSingleton() { }

        private static readonly Lazy<HabitCheckInSingleton> lazy = new(() => new HabitCheckInSingleton());

        public static HabitCheckInSingleton Instance => lazy.Value;
    }
}
