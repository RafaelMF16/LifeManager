namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>A goal over the months of the period that had it.</summary>
    /// <param name="Goal">The goals of those months, summed.</param>
    /// <param name="Actual">What was actually spent or invested in those same months.</param>
    /// <param name="Ratio">Actual / goal (1 = exactly the goal); null when no month had the goal.</param>
    /// <param name="MonthsAchieved">Months within the limit (expense) or at least at the target (investment).</param>
    public record DashboardBudgetSummaryDto(decimal Goal, decimal Actual, decimal? Ratio, int MonthsWithGoal, int MonthsAchieved);
}
