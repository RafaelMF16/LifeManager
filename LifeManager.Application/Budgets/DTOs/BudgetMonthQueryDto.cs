namespace LifeManager.Application.Budgets.DTOs
{
    /// <summary>Query-string parameters of <c>GET /api/Budgets</c> and <c>DELETE /api/Budgets/{id}</c>.</summary>
    public record BudgetMonthQueryDto
    {
        /// <summary>"yyyy-MM": the month to read, or the first month a goal is removed from.</summary>
        public string? Month { get; init; }
    }
}
