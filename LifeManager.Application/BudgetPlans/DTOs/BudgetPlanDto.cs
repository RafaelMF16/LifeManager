namespace LifeManager.Application.BudgetPlans.DTOs
{
    /// <summary>Body of <c>PUT /api/BudgetPlans/{month}</c>: the month's whole plan, replacing any stored one.</summary>
    /// <param name="Slices">At most 100% in all; missing means none (the whole total is free).</param>
    public record BudgetPlanDto(decimal Total, IReadOnlyList<BudgetSliceDto>? Slices);
}
