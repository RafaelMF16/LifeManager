using LifeManager.Application.Habits.DTOs;
using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Application.Transactions.DTOs
{
    /// <summary>
    /// Response of <c>POST /api/Transactions</c>: the same fields as <see cref="TransactionResponseDto"/>, plus the month
    /// the transaction fell into.
    /// </summary>
    /// <param name="MonthCreated">
    /// Whether the month didn't exist and was opened by this request. Two requests racing to open the same month may
    /// both say true; only one month is ever stored.
    /// </param>
    /// <param name="SavingsGoalId">The savings goal this investment counts towards; null for none.</param>
    /// <param name="AutoCheckIns">The habits the new transaction checked in by themselves (empty when none).</param>
    public record QuickTransactionResponseDto(
        int Id,
        MoneyFlowType Type,
        int CategoryId,
        string CategoryName,
        decimal Amount,
        string Description,
        DateOnly Date,
        int? RecurringTransactionId,
        int MonthlySummaryId,
        bool MonthCreated,
        IReadOnlyList<AutoCheckInDto> AutoCheckIns,
        int? SavingsGoalId = null);
}
