using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Application.Transactions.DTOs
{
    /// <param name="RecurringTransactionId">The recurrence that posted it; null for transactions entered by hand.</param>
    public record TransactionResponseDto(
        int Id,
        MoneyFlowType Type,
        int CategoryId,
        string CategoryName,
        decimal Amount,
        string Description,
        DateOnly Date,
        int? RecurringTransactionId = null);
}
