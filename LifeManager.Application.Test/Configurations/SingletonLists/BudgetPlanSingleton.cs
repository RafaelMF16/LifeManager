using LifeManager.Domain.BudgetPlans;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class BudgetPlanSingleton : List<BudgetPlan>
    {
        private BudgetPlanSingleton() { }

        private static readonly Lazy<BudgetPlanSingleton> lazy = new(() => new BudgetPlanSingleton());

        public static BudgetPlanSingleton Instance => lazy.Value;
    }
}
