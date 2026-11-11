using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Application.Transactions.DTOs
{
    /// <summary>Body of <c>POST</c>/<c>PUT /api/MonthlySummaries/{monthlySummaryId}/Transactions</c> and <c>POST /api/Transactions</c>.</summary>
    /// <param name="SavingsGoalId">
    /// The user's active savings goal this investment counts towards; only for <see cref="MoneyFlowType.Investment"/>.
    /// Left out (or null) on an update unlinks the transaction.
    /// </param>
    public record TransactionDto(MoneyFlowType Type, int CategoryId, decimal Amount, string Description, DateOnly Date, int? SavingsGoalId = null);
}
