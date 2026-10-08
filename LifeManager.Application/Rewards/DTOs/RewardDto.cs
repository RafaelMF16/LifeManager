namespace LifeManager.Application.Rewards.DTOs
{
    /// <summary>Body of <c>POST /api/Rewards</c> and <c>PUT /api/Rewards/{id}</c>.</summary>
    /// <param name="Cost">In coins.</param>
    /// <param name="Icon">The id of an icon the frontend knows; optional.</param>
    public record RewardDto(string Name, int Cost, string? Icon);
}
