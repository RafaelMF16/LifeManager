namespace LifeManager.Application.MonthlySummaries.DTOs
{
    public record MonthlySummaryResponseDto(int Id, int Month, int Year, decimal TotalIncome, decimal TotalExpense, decimal Balance);
}
