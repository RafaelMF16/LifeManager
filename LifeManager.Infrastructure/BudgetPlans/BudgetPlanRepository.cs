using LifeManager.Domain.BudgetPlans;
using LifeManager.Domain.BudgetPlans.Interfaces;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.BudgetPlans
{
    public class BudgetPlanRepository(LifeManagerDbContext dbContext) : IBudgetPlanRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<BudgetPlanListItem?> GetAsync(UserId userId, YearMonth month, CancellationToken cancellationToken)
        {
            var monthStart = month.FirstDay;

            var plan = await _dbContext.BudgetPlans
                .AsNoTracking()
                .SingleOrDefaultAsync(plan => plan.UserId == userId && plan.MonthStart == monthStart, cancellationToken);

            if (plan is null)
                return null;

            return new BudgetPlanListItem(plan, await CategoryNamesAsync(userId, cancellationToken));
        }

        public async Task<YearMonth?> GetLatestMonthBeforeAsync(UserId userId, YearMonth month, CancellationToken cancellationToken)
        {
            var monthStart = month.FirstDay;

            var latest = await _dbContext.BudgetPlans
                .AsNoTracking()
                .Where(plan => plan.UserId == userId && plan.MonthStart < monthStart)
                .OrderByDescending(plan => plan.MonthStart)
                .Select(plan => (DateOnly?)plan.MonthStart)
                .FirstOrDefaultAsync(cancellationToken);

            return latest is null ? null : YearMonth.From(latest.Value);
        }

        public async Task<IReadOnlyList<BudgetPlanListItem>> GetOverlappingAsync(UserId userId, YearMonth from, YearMonth to, CancellationToken cancellationToken)
        {
            var firstMonth = from.FirstDay;
            var lastMonth = to.FirstDay;

            var plans = await _dbContext.BudgetPlans
                .AsNoTracking()
                .Where(plan => plan.UserId == userId && plan.MonthStart >= firstMonth && plan.MonthStart <= lastMonth)
                .OrderBy(plan => plan.MonthStart)
                .ToListAsync(cancellationToken);

            if (plans.Count == 0)
                return [];

            var categoryNames = await CategoryNamesAsync(userId, cancellationToken);

            return [.. plans.Select(plan => new BudgetPlanListItem(plan, categoryNames))];
        }

        public async Task SaveAsync(BudgetPlan plan, CancellationToken cancellationToken)
        {
            var stored = await _dbContext.BudgetPlans
                .SingleOrDefaultAsync(storedPlan => storedPlan.UserId == plan.UserId && storedPlan.MonthStart == plan.MonthStart, cancellationToken);

            // Replacing the tracked plan's slices makes EF delete the stored ones and insert the new ones.
            if (stored is null)
                _dbContext.Add(plan);
            else
                stored.ReplaceWith(plan);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> DeleteAsync(UserId userId, YearMonth month, CancellationToken cancellationToken)
        {
            var monthStart = month.FirstDay;

            var deleted = await _dbContext.BudgetPlans
                .Where(plan => plan.UserId == userId && plan.MonthStart == monthStart)
                .ExecuteDeleteAsync(cancellationToken);

            return deleted > 0;
        }

        // Read on their own, like the dashboard does: a user's category list is small.
        private async Task<IReadOnlyDictionary<int, string>> CategoryNamesAsync(UserId userId, CancellationToken cancellationToken)
        {
            var categories = await _dbContext.Categories
                .AsNoTracking()
                .Where(category => category.UserId == userId)
                .Select(category => new { category.Id, category.Name })
                .ToListAsync(cancellationToken);

            return categories.ToDictionary(category => category.Id!.Value, category => category.Name.Value);
        }
    }
}
