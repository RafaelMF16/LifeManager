namespace LifeManager.Application.Rewards.DTOs
{
    /// <summary>Body of <c>POST /api/Rewards</c> and <c>PUT /api/Rewards/{id}</c>.</summary>
    /// <param name="Cost">In coins.</param>
    /// <param name="Icon">The id of an icon the frontend knows; optional.</param>
    /// <param name="ExpenseCategoryId">The category of the real expense the reward stands for; with <paramref name="ExpenseAmount"/> or neither.</param>
    /// <param name="ExpenseAmount">What the reward costs in money; null (with no category) for a reward without an expense.</param>
    public record RewardDto(string Name, int Cost, string? Icon, int? ExpenseCategoryId = null, decimal? ExpenseAmount = null);
}
