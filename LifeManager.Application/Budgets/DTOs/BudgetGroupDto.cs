namespace LifeManager.Application.Budgets.DTOs
{
    /// <param name="Actual">The month's whole total of the type, with or without a goal on it.</param>
    /// <param name="Total">The goal on the month's total; null when there is none.</param>
    /// <param name="Categories">Per-category goals, by category name.</param>
    public record BudgetGroupDto(decimal Actual, BudgetProgressDto? Total, IReadOnlyList<BudgetProgressDto> Categories);
}
