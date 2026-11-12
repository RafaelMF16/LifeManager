using LifeManager.Domain.SavingsGoals;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class SavingsGoalSingleton : List<SavingsGoal>
    {
        private SavingsGoalSingleton() { }

        private static readonly Lazy<SavingsGoalSingleton> lazy = new(() => new SavingsGoalSingleton());

        public static SavingsGoalSingleton Instance => lazy.Value;
    }
}
