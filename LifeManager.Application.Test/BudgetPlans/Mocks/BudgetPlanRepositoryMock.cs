using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.BudgetPlans;
using LifeManager.Domain.BudgetPlans.Interfaces;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.BudgetPlans.Mocks
{
    public class BudgetPlanRepositoryMock : IBudgetPlanRepository
    {
        private readonly BudgetPlanSingleton _instance;

        public BudgetPlanRepositoryMock()
        {
            _instance = BudgetPlanSingleton.Instance;
        }

        public Task<BudgetPlanListItem?> GetAsync(UserId userId, YearMonth month, CancellationToken cancellationToken)
        {
            var stored = _instance.SingleOrDefault(plan => plan.UserId == userId && plan.MonthStart == month.FirstDay);

            return Task.FromResult(stored is null ? null : new BudgetPlanListItem(ToDetachedCopy(stored), CategoryNames(userId)));
        }

        public Task<YearMonth?> GetLatestMonthBeforeAsync(UserId userId, YearMonth month, CancellationToken cancellationToken)
        {
            var latest = _instance
                .Where(plan => plan.UserId == userId && plan.MonthStart < month.FirstDay)
                .OrderByDescending(plan => plan.MonthStart)
                .FirstOrDefault();

            return Task.FromResult(latest?.Month);
        }

        // Mirrors BudgetPlanRepository.GetOverlappingAsync: both ends included, oldest first, names from the user's categories.
        public Task<IReadOnlyList<BudgetPlanListItem>> GetOverlappingAsync(UserId userId, YearMonth from, YearMonth to, CancellationToken cancellationToken)
        {
            var names = CategoryNames(userId);

            IReadOnlyList<BudgetPlanListItem> items = [.. _instance
                .Where(plan => plan.UserId == userId && plan.MonthStart >= from.FirstDay && plan.MonthStart <= to.FirstDay)
                .OrderBy(plan => plan.MonthStart)
                .Select(plan => new BudgetPlanListItem(ToDetachedCopy(plan), names))];

            return Task.FromResult(items);
        }

        // Mirrors the unique (UserId, MonthStart) index and the replace-by-id of BudgetPlanRepository.SaveAsync.
        public Task SaveAsync(BudgetPlan plan, CancellationToken cancellationToken)
        {
            if (plan.Id is null)
            {
                if (_instance.Any(stored => stored.UserId == plan.UserId && stored.MonthStart == plan.MonthStart))
                    throw new InvalidOperationException("Duplicate plan for the same month");

                plan.AssignId(_instance.Count == 0 ? 1 : _instance.Max(stored => stored.Id!.Value) + 1);
                _instance.Add(ToDetachedCopy(plan));
                return Task.CompletedTask;
            }

            var index = _instance.FindIndex(stored => stored.Id == plan.Id && stored.UserId == plan.UserId);
            _instance[index] = ToDetachedCopy(plan);

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(UserId userId, YearMonth month, CancellationToken cancellationToken)
            => Task.FromResult(_instance.RemoveAll(plan => plan.UserId == userId && plan.MonthStart == month.FirstDay) > 0);

        private static IReadOnlyDictionary<int, string> CategoryNames(UserId userId)
            => CategorySingleton.Instance
                .Where(category => category.UserId == userId)
                .ToDictionary(category => category.Id!.Value, category => category.Name.Value);

        // Slices are immutable, so the copy can share them.
        private static BudgetPlan ToDetachedCopy(BudgetPlan plan)
        {
            var copy = BudgetPlan.Create(plan.UserId.Value, plan.Month, plan.Total.Value, plan.Slices).Value!;
            copy.AssignId(plan.Id!.Value);

            return copy;
        }
    }
}
