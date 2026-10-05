namespace LifeManager.Application.MonthlySummaries.DTOs
{
    /// <summary>
    /// One month as shown on its own screen: totals, how many transactions of each type it has, and the user's
    /// months right before and after it (null when there is none).
    /// </summary>
    public record MonthlySummaryDetailsResponseDto(
        int Id,
        int Month,
        int Year,
        decimal TotalIncome,
        decimal TotalExpense,
        decimal TotalInvestment,
        decimal Balance,
        int IncomeCount,
        int ExpenseCount,
        int InvestmentCount,
        int? PreviousId,
        int? NextId);
}
