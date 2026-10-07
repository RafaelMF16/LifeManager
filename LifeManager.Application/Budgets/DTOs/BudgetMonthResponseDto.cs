namespace LifeManager.Application.Budgets.DTOs
{
    /// <summary>The goals in force in one month, each with what was actually spent or invested.</summary>
    /// <param name="Month">"yyyy-MM".</param>
    public record BudgetMonthResponseDto(string Month, BudgetGroupDto Expenses, BudgetGroupDto Investments);
}
