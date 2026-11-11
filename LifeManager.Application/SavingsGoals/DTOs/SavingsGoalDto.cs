namespace LifeManager.Application.SavingsGoals.DTOs
{
    /// <summary>Body of <c>POST /api/SavingsGoals</c> and <c>PUT /api/SavingsGoals/{id}</c>.</summary>
    /// <param name="TargetMonth">The deadline as "yyyy-MM", not before the current month; null or empty for no deadline.</param>
    /// <param name="InitialAmount">What was already saved before the goal; zero or more.</param>
    /// <param name="Icon">The id of an icon the frontend knows; optional.</param>
    public record SavingsGoalDto(string Name, decimal TargetAmount, string? TargetMonth = null, decimal InitialAmount = 0m, string? Icon = null);
}
