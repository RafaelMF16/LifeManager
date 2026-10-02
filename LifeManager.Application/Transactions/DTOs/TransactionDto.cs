using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Application.Transactions.DTOs
{
    /// <summary>Body of <c>POST</c>/<c>PUT /api/MonthlySummaries/{monthlySummaryId}/Transactions</c>.</summary>
    public record TransactionDto(MoneyFlowType Type, int CategoryId, decimal Amount, string Description, DateOnly Date);
}
