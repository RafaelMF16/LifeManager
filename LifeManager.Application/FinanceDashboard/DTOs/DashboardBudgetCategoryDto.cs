namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>One category's goal over the months of the period that had it.</summary>
    /// <param name="Ratio">Actual / goal (1 = exactly the goal).</param>
    /// <param name="Gap">Summed over the months: what was spent over the limit (expense) or missing to the target (investment).</param>
    public record DashboardBudgetCategoryDto(
        int CategoryId,
        string Name,
        decimal Goal,
        decimal Actual,
        decimal Ratio,
        decimal Gap,
        int MonthsWithGoal,
        int MonthsAchieved);
}
