namespace LifeManager.Domain.Budgets
{
    /// <summary>A goal version plus its category's name (null for a goal on the month's total).</summary>
    public record BudgetListItem(Budget Budget, string? CategoryName);
}
