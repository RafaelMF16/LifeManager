using LifeManager.Domain.Budgets;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class BudgetSingleton : List<Budget>
    {
        private BudgetSingleton() { }

        private static readonly Lazy<BudgetSingleton> lazy = new(() => new BudgetSingleton());

        public static BudgetSingleton Instance => lazy.Value;
    }
}
