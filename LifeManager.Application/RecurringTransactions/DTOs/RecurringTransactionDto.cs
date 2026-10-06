using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Application.RecurringTransactions.DTOs
{
    /// <summary>Body of <c>POST</c>/<c>PUT /api/RecurringTransactions</c>.</summary>
    /// <param name="StartMonth">First month with an occurrence, "yyyy-MM".</param>
    /// <param name="EndMonth">Last month with an occurrence, "yyyy-MM"; null means it never ends.</param>
    public record RecurringTransactionDto(
        MoneyFlowType Type,
        int CategoryId,
        decimal Amount,
        string Description,
        int DayOfMonth,
        string? StartMonth,
        string? EndMonth);
}
