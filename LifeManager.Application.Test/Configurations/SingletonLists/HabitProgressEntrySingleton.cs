using LifeManager.Domain.Habits;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class HabitProgressEntrySingleton : List<HabitProgressEntry>
    {
        private HabitProgressEntrySingleton() { }

        private static readonly Lazy<HabitProgressEntrySingleton> lazy = new(() => new HabitProgressEntrySingleton());

        public static HabitProgressEntrySingleton Instance => lazy.Value;
    }
}
