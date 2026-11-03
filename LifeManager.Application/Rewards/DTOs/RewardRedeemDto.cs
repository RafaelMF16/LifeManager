namespace LifeManager.Application.Rewards.DTOs
{
    /// <param name="RegisterExpense">Also posts the reward's expense (category and amount) as today's transaction.</param>
    public record RewardRedeemDto(bool RegisterExpense = false);
}
