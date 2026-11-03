using System.Text.Json.Serialization;
using LifeManager.Application.Habits.DTOs;
using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Application.Transactions.DTOs
{
    /// <param name="RecurringTransactionId">The recurrence that posted it; null for transactions entered by hand.</param>
    /// <param name="AutoCheckIns">
    /// Only on create: the habits the new transaction checked in by themselves (empty when none). Left out of every
    /// other response.
    /// </param>
    public record TransactionResponseDto(
        int Id,
        MoneyFlowType Type,
        int CategoryId,
        string CategoryName,
        decimal Amount,
        string Description,
        DateOnly Date,
        int? RecurringTransactionId = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<AutoCheckInDto>? AutoCheckIns = null);
}
