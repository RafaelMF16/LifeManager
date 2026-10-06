using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Budgets;
using LifeManager.Domain.Budgets.Interfaces;
using LifeManager.Domain.Budgets.ValueObjects;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Budgets.Mocks
{
    public class BudgetRepositoryMock : IBudgetRepository
    {
        private readonly BudgetSingleton _instance;

        public BudgetRepositoryMock()
        {
            _instance = BudgetSingleton.Instance;
        }

        public Task<Budget?> GetByIdAsync(BudgetId budgetId, UserId userId, CancellationToken cancellationToken)
        {
            var stored = _instance.SingleOrDefault(budget => budget.Id == budgetId && budget.UserId == userId);

            return Task.FromResult(stored is null ? null : ToDetachedCopy(stored));
        }

        public Task<IReadOnlyList<Budget>> GetVersionsAsync(UserId userId, MoneyFlowType type, CategoryId? categoryId, CancellationToken cancellationToken)
        {
            IReadOnlyList<Budget> versions = [.. _instance
                .Where(budget => budget.UserId == userId && budget.Type == type && Equals(budget.CategoryId, categoryId))
                .OrderBy(budget => budget.EffectiveFrom)
                .Select(ToDetachedCopy)];

            return Task.FromResult(versions);
        }

        // Mirrors BudgetRepository.GetOverlappingAsync: the same date comparisons and order, names from the user's categories.
        public Task<IReadOnlyList<BudgetListItem>> GetOverlappingAsync(UserId userId, YearMonth from, YearMonth to, CancellationToken cancellationToken)
        {
            var namesById = CategorySingleton.Instance
                .Where(category => category.UserId == userId)
                .ToDictionary(category => category.Id!.Value, category => category.Name.Value);

            IReadOnlyList<BudgetListItem> items = [.. _instance
                .Where(budget => budget.UserId == userId
                    && budget.EffectiveFrom <= to.FirstDay
                    && (budget.EffectiveTo == null || budget.EffectiveTo >= from.FirstDay))
                .OrderBy(budget => budget.EffectiveFrom)
                .ThenBy(budget => budget.Id!.Value)
                .Select(budget => new BudgetListItem(
                    ToDetachedCopy(budget),
                    budget.CategoryId is null ? null : namesById.GetValueOrDefault(budget.CategoryId.Value)))];

            return Task.FromResult(items);
        }

        // Mirrors BudgetRepository.ApplyChangeAsync, and the unique (UserId, Type, CategoryId, EffectiveFrom) index.
        public Task ApplyChangeAsync(BudgetTimelineChange change, CancellationToken cancellationToken)
        {
            foreach (var deletedId in change.DeletedIds)
                _instance.RemoveAll(budget => budget.Id == deletedId);

            foreach (var updated in change.Updated)
            {
                var index = _instance.FindIndex(budget => budget.Id == updated.Id && budget.UserId == updated.UserId);
                if (index >= 0)
                    _instance[index] = ToDetachedCopy(updated);
            }

            if (change.Added is not null)
            {
                var added = change.Added;
                if (_instance.Any(budget => budget.UserId == added.UserId && budget.Type == added.Type && Equals(budget.CategoryId, added.CategoryId) && budget.EffectiveFrom == added.EffectiveFrom))
                    throw new InvalidOperationException("Duplicate goal version for the same month");

                var newId = _instance.Count == 0 ? 1 : _instance.Max(budget => budget.Id!.Value) + 1;
                added.AssignId(newId);
                _instance.Add(ToDetachedCopy(added));
            }

            return Task.CompletedTask;
        }

        private static Budget ToDetachedCopy(Budget budget)
            => Budget.FromPersistence(
                budget.Id!.Value,
                budget.UserId.Value,
                budget.Type,
                budget.CategoryId?.Value,
                budget.Amount.Value,
                budget.EffectiveFrom,
                budget.EffectiveTo);
    }
}
