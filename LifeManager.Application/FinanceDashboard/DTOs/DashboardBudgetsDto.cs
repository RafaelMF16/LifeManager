namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>
    /// How the period went against the user's goals. Only the period itself is evaluated (no comparison). Every month
    /// counts, including one still in progress: only the client knows the user's current month and can tell it apart.
    /// </summary>
    /// <param name="HasGoals">Whether any goal was in force in the period; when false everything else is empty or zero.</param>
    /// <param name="Months">One row per month of the period, in the same order as <see cref="FinanceDashboardResponseDto.Months"/>.</param>
    /// <param name="Expense">The month-total spending limits.</param>
    /// <param name="Investment">The month-total investment targets.</param>
    /// <param name="ExpenseCategories">Categories with a spending limit, most overspent first (at most <c>TopBudgetCategoryCount</c>).</param>
    /// <param name="InvestmentCategories">Categories with an investment target, furthest from it first (at most <c>TopBudgetCategoryCount</c>).</param>
    public record DashboardBudgetsDto(
        bool HasGoals,
        IReadOnlyList<DashboardBudgetMonthDto> Months,
        DashboardBudgetSummaryDto Expense,
        DashboardBudgetSummaryDto Investment,
        IReadOnlyList<DashboardBudgetCategoryDto> ExpenseCategories,
        IReadOnlyList<DashboardBudgetCategoryDto> InvestmentCategories);
}
