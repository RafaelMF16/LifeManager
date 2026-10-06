namespace LifeManager.Application.Budgets.DTOs
{
    /// <summary>One goal against the month's actual amount. Amounts are positive.</summary>
    /// <param name="Id">The version in force this month (what a removal from this month refers to).</param>
    /// <param name="Remaining">Goal − actual: negative when an expense limit was passed or an investment target exceeded.</param>
    /// <param name="Ratio">Actual / goal (1 = exactly the goal).</param>
    /// <param name="Achieved">Within the limit (expense) or at least the target (investment).</param>
    /// <param name="EffectiveFrom">First month of this version, "yyyy-MM".</param>
    /// <param name="EffectiveTo">Last month of this version, "yyyy-MM"; null while open-ended.</param>
    public record BudgetProgressDto(
        int Id,
        int? CategoryId,
        string? CategoryName,
        decimal Goal,
        decimal Actual,
        decimal Remaining,
        decimal Ratio,
        bool Achieved,
        string EffectiveFrom,
        string? EffectiveTo);
}
