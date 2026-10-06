using LifeManager.Domain.Budgets;
using LifeManager.Domain.Budgets.Interfaces;
using LifeManager.Domain.Budgets.ValueObjects;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Budgets
{
    public class BudgetRepository(LifeManagerDbContext dbContext) : IBudgetRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<Budget?> GetByIdAsync(BudgetId budgetId, UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.Budgets
                .AsNoTracking()
                .SingleOrDefaultAsync(budget => budget.Id == budgetId && budget.UserId == userId, cancellationToken);
        }

        public async Task<IReadOnlyList<Budget>> GetVersionsAsync(UserId userId, MoneyFlowType type, CategoryId? categoryId, CancellationToken cancellationToken)
        {
            var query = _dbContext.Budgets
                .AsNoTracking()
                .Where(budget => budget.UserId == userId && budget.Type == type);

            query = categoryId is null
                ? query.Where(budget => budget.CategoryId == null)
                : query.Where(budget => budget.CategoryId == categoryId);

            return await query
                .OrderBy(budget => budget.EffectiveFrom)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<BudgetListItem>> GetOverlappingAsync(UserId userId, YearMonth from, YearMonth to, CancellationToken cancellationToken)
        {
            var firstMonth = from.FirstDay;
            var lastMonth = to.FirstDay;

            var budgets = await _dbContext.Budgets
                .AsNoTracking()
                .Where(budget => budget.UserId == userId
                    && budget.EffectiveFrom <= lastMonth
                    && (budget.EffectiveTo == null || budget.EffectiveTo >= firstMonth))
                .OrderBy(budget => budget.EffectiveFrom)
                .ThenBy(budget => budget.Id)
                .ToListAsync(cancellationToken);

            if (budgets.Count == 0)
                return [];

            // Names are read on their own, like the dashboard does: some goals have no category, and a user's
            // category list is small.
            var categoryNames = await _dbContext.Categories
                .AsNoTracking()
                .Where(category => category.UserId == userId)
                .Select(category => new { category.Id, category.Name })
                .ToListAsync(cancellationToken);
            var namesById = categoryNames.ToDictionary(category => category.Id!.Value, category => category.Name.Value);

            return [.. budgets.Select(budget => new BudgetListItem(
                budget,
                budget.CategoryId is null ? null : namesById.GetValueOrDefault(budget.CategoryId.Value)))];
        }

        public async Task ApplyChangeAsync(BudgetTimelineChange change, CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // Deletes first, so the added version never collides with a replaced one on the unique index.
            foreach (var deletedId in change.DeletedIds)
            {
                await _dbContext.Budgets
                    .Where(budget => budget.Id == deletedId)
                    .ExecuteDeleteAsync(cancellationToken);
            }

            foreach (var updated in change.Updated)
            {
                await _dbContext.Budgets
                    .Where(budget => budget.Id == updated.Id && budget.UserId == updated.UserId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(budget => budget.Amount, updated.Amount)
                        .SetProperty(budget => budget.EffectiveTo, updated.EffectiveTo), cancellationToken);
            }

            if (change.Added is not null)
            {
                _dbContext.Add(change.Added);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await databaseTransaction.CommitAsync(cancellationToken);
        }
    }
}
