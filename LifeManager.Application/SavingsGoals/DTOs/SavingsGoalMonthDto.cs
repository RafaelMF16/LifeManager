namespace LifeManager.Application.SavingsGoals.DTOs
{
    /// <param name="Month">"yyyy-MM".</param>
    /// <param name="Amount">The goal's linked investments dated in that month (0 when none).</param>
    public record SavingsGoalMonthDto(string Month, decimal Amount);
}
