using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Application.Budgets.DTOs
{
    /// <summary>Body of <c>PUT /api/Budgets</c>: sets the goal from <paramref name="From"/> on.</summary>
    /// <param name="Type">Expense (a spending limit) or Investment (a target).</param>
    /// <param name="CategoryId">Null for a goal on the month's total.</param>
    /// <param name="From">First month the amount applies to, "yyyy-MM".</param>
    public record BudgetDto(MoneyFlowType Type, int? CategoryId, string? From, decimal Amount);
}
