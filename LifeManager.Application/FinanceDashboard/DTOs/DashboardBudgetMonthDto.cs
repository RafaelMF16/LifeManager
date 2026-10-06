namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>A month's goals on its totals; each goal and its "achieved" flag are null when the month had none.</summary>
    public record DashboardBudgetMonthDto(
        int Year,
        int Month,
        decimal? ExpenseGoal,
        bool? ExpenseAchieved,
        decimal? InvestmentGoal,
        bool? InvestmentAchieved);
}
