namespace LifeManager.Application.SavingsGoals.DTOs
{
    /// <summary>A transaction linked to a savings goal, with its month so the frontend can link to it.</summary>
    public record SavingsGoalTransactionDto(
        int Id,
        int MonthlySummaryId,
        int CategoryId,
        string CategoryName,
        decimal Amount,
        string Description,
        DateOnly Date);
}
