namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>
    /// How the period went against the user's goals. Only the period itself is evaluated (no comparison). Every month
    /// counts, including one still in progress: only the client knows the user's current month and can tell it apart.
    /// </summary>
    /// <param name="HasGoals">Whether any month of the period has a budget plan; when false everything else is empty or zero.</param>
    /// <param name="Months">One row per month of the period, in the same order as <see cref="FinanceDashboardResponseDto.Months"/>.</param>
    /// <param name="Expense">The months' spending limits: each plan's total minus its investment slice.</param>
    /// <param name="Investment">The months' investment slices.</param>
    /// <param name="ExpenseCategories">Categories with an expense slice, most overspent first (at most <c>TopBudgetCategoryCount</c>).</param>
    /// <param name="InvestmentCategories">Always empty: a plan has one investment slice, not one per category. Kept for the contract.</param>
    public record DashboardBudgetsDto(
        bool HasGoals,
        IReadOnlyList<DashboardBudgetMonthDto> Months,
        DashboardBudgetSummaryDto Expense,
        DashboardBudgetSummaryDto Investment,
        IReadOnlyList<DashboardBudgetCategoryDto> ExpenseCategories,
        IReadOnlyList<DashboardBudgetCategoryDto> InvestmentCategories);
}
