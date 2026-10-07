using LifeManager.Domain.Budgets.ValueObjects;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Budgets.Interfaces
{
    public interface IBudgetRepository
    {
        Task<Budget?> GetByIdAsync(BudgetId budgetId, UserId userId, CancellationToken cancellationToken);

        /// <summary>Every version of one goal, oldest first.</summary>
        /// <param name="categoryId">Null for the goal on the month's total.</param>
        Task<IReadOnlyList<Budget>> GetVersionsAsync(UserId userId, MoneyFlowType type, CategoryId? categoryId, CancellationToken cancellationToken);

        /// <summary>The user's versions in force at some point between <paramref name="from"/> and <paramref name="to"/> (both included).</summary>
        Task<IReadOnlyList<BudgetListItem>> GetOverlappingAsync(UserId userId, YearMonth from, YearMonth to, CancellationToken cancellationToken);

        /// <summary>Deletes, rewrites and adds the versions of one goal in a single database transaction.</summary>
        Task ApplyChangeAsync(BudgetTimelineChange change, CancellationToken cancellationToken);
    }
}
