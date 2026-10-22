using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Application.BudgetPlans.DTOs
{
    /// <param name="Type">Expense (a spending limit) or Investment (the month's target).</param>
    /// <param name="CategoryId">Required for an expense; null for the investment slice.</param>
    /// <param name="Percent">Share of the total, greater than 0 and at most 100, 2 decimal places.</param>
    public record BudgetSliceDto(MoneyFlowType Type, int? CategoryId, decimal Percent);
}
