namespace LifeManager.Application.BudgetPlans.DTOs
{
    /// <summary>A month's plan with what was actually spent and invested; an empty month when it has no plan.</summary>
    /// <param name="Month">"yyyy-MM".</param>
    /// <param name="Exists">Whether the month has a plan; when false there is no total and no slice.</param>
    /// <param name="Slices">Expense slices by category name, then the investment slice.</param>
    /// <param name="FreePercent">What the slices leave (100 without a plan).</param>
    /// <param name="FreeAmount">The free part in money, closing with the total; null without a plan.</param>
    /// <param name="FreeActual">Spent in the month in categories without a slice (everything spent, without a plan).</param>
    /// <param name="CopySource">The latest earlier month with a plan ("yyyy-MM"), to offer copying it; null when there is none.</param>
    public record BudgetPlanResponseDto(
        string Month,
        bool Exists,
        decimal? Total,
        IReadOnlyList<BudgetSliceProgressDto> Slices,
        decimal FreePercent,
        decimal? FreeAmount,
        decimal FreeActual,
        string? CopySource);
}
