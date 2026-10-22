namespace LifeManager.Domain.BudgetPlans
{
    /// <summary>A plan plus the names of its slices' categories, by category id.</summary>
    public record BudgetPlanListItem(BudgetPlan Plan, IReadOnlyDictionary<int, string> CategoryNames);
}
