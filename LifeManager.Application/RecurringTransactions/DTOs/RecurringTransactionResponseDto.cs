using LifeManager.Domain.RecurringTransactions.Enums;
using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Application.RecurringTransactions.DTOs
{
    /// <param name="StartMonth">"yyyy-MM".</param>
    /// <param name="EndMonth">"yyyy-MM"; null means it never ends.</param>
    /// <param name="NextOccurrenceDate">When the next occurrence is posted; null once finished.</param>
    public record RecurringTransactionResponseDto(
        int Id,
        MoneyFlowType Type,
        int CategoryId,
        string CategoryName,
        decimal Amount,
        string Description,
        int DayOfMonth,
        string StartMonth,
        string? EndMonth,
        RecurringTransactionStatus Status,
        DateOnly? NextOccurrenceDate);
}
