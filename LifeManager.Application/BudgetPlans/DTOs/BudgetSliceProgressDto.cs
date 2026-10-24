using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Application.BudgetPlans.DTOs
{
    /// <summary>One slice of a month's plan against what was actually spent (expense) or invested. Amounts are positive.</summary>
    /// <param name="CategoryId">The expense's category; null for the investment slice.</param>
    /// <param name="Percent">The slice's share of the total (25 = 25%).</param>
    /// <param name="Amount">The slice in money: total × percent, rounded to cents.</param>
    /// <param name="Actual">Spent in the category, or everything invested in the month for the investment slice.</param>
    /// <param name="Ratio">Actual / amount (1 = exactly the slice).</param>
    /// <param name="Achieved">Within the limit (expense) or at least the target (investment).</param>
    public record BudgetSliceProgressDto(
        MoneyFlowType Type,
        int? CategoryId,
        string? CategoryName,
        decimal Percent,
        decimal Amount,
        decimal Actual,
        decimal Ratio,
        bool Achieved);
}
