using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Application.Budgets.DTOs
{
    /// <param name="EffectiveFrom">First month of this version, "yyyy-MM".</param>
    /// <param name="EffectiveTo">Last month of this version, "yyyy-MM"; null while open-ended.</param>
    public record BudgetResponseDto(
        int Id,
        MoneyFlowType Type,
        int? CategoryId,
        string? CategoryName,
        decimal Amount,
        string EffectiveFrom,
        string? EffectiveTo);
}
