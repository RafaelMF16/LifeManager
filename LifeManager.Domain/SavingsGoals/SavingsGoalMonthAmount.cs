using LifeManager.Domain.Shared.ValueObjects;

namespace LifeManager.Domain.SavingsGoals
{
    /// <summary>What a goal's linked investments add up to in one month (months without any are left out).</summary>
    public sealed record SavingsGoalMonthAmount(YearMonth Month, decimal Amount);
}
