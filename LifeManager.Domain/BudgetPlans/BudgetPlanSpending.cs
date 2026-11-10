namespace LifeManager.Domain.BudgetPlans
{
    /// <summary>A month's plan with what was spent in each expense slice's category, for the Home.</summary>
    /// <param name="ExpenseSlices">One per expense slice, in no particular order.</param>
    public record BudgetPlanSpending(BudgetPlan Plan, IReadOnlyList<BudgetSliceSpending> ExpenseSlices);

    /// <param name="Spent">The month's expenses in the category (positive; 0 when none).</param>
    public record BudgetSliceSpending(int CategoryId, string CategoryName, decimal Spent);
}
