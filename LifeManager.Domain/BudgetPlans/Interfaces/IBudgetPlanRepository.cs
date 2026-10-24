using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.BudgetPlans.Interfaces
{
    public interface IBudgetPlanRepository
    {
        /// <summary>The user's plan for <paramref name="month"/>, if there is one.</summary>
        Task<BudgetPlanListItem?> GetAsync(UserId userId, YearMonth month, CancellationToken cancellationToken);

        /// <summary>The latest month before <paramref name="month"/> that has a plan, if any.</summary>
        Task<YearMonth?> GetLatestMonthBeforeAsync(UserId userId, YearMonth month, CancellationToken cancellationToken);

        /// <summary>The user's plans from <paramref name="from"/> to <paramref name="to"/> (both included), oldest first.</summary>
        Task<IReadOnlyList<BudgetPlanListItem>> GetOverlappingAsync(UserId userId, YearMonth from, YearMonth to, CancellationToken cancellationToken);

        /// <summary>Stores <paramref name="plan"/> as its month's: adds it, or replaces the stored plan of the same user and month with its total and slices.</summary>
        Task SaveAsync(BudgetPlan plan, CancellationToken cancellationToken);

        /// <returns>Whether there was a plan to delete.</returns>
        Task<bool> DeleteAsync(UserId userId, YearMonth month, CancellationToken cancellationToken);
    }
}
