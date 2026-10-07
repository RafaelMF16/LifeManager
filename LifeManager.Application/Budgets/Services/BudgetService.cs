using LifeManager.Application.Budgets.DTOs;
using LifeManager.Domain.Budgets;
using LifeManager.Domain.Budgets.Errors;
using LifeManager.Domain.Budgets.Interfaces;
using LifeManager.Domain.Budgets.ValueObjects;
using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.Categories.Interfaces;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.FinanceDashboard;
using LifeManager.Domain.FinanceDashboard.Interfaces;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Budgets.Services
{
    /// <remarks>
    /// Goals are versioned by month (see <see cref="BudgetTimeline"/>): setting or removing one applies from a month on
    /// and never changes the goal earlier months had. The actual amounts come from the same per-category monthly sums
    /// the dashboard reads.
    /// </remarks>
    public class BudgetService(
        IBudgetRepository budgetRepository,
        ICategoryRepository categoryRepository,
        IFinanceDashboardRepository financeDashboardRepository)
    {
        private readonly IBudgetRepository _budgetRepository = budgetRepository;
        private readonly ICategoryRepository _categoryRepository = categoryRepository;
        private readonly IFinanceDashboardRepository _financeDashboardRepository = financeDashboardRepository;

        public async Task<Result<BudgetMonthResponseDto>> GetMonthAsync(BudgetMonthQueryDto query, UserId userId, CancellationToken cancellationToken)
        {
            if (!YearMonth.TryParse(query.Month, out var month))
                return BudgetErrors.InvalidMonth;

            var budgets = await _budgetRepository.GetOverlappingAsync(userId, month, month, cancellationToken);
            var totals = await _financeDashboardRepository.GetCategoryMonthTotalsAsync(userId, month.FirstDay, month.LastDay, cancellationToken);

            return new BudgetMonthResponseDto(
                month.ToString(),
                ToGroupDto(MoneyFlowType.Expense, budgets, totals),
                ToGroupDto(MoneyFlowType.Investment, budgets, totals));
        }

        public async Task<Result<BudgetResponseDto>> SetAsync(BudgetDto budgetDto, UserId userId, CancellationToken cancellationToken)
        {
            if (!YearMonth.TryParse(budgetDto.From, out var from))
                return BudgetErrors.InvalidMonth;

            var budgetResult = Budget.Create(userId.Value, budgetDto.Type, budgetDto.CategoryId, budgetDto.Amount, from);
            if (!budgetResult.IsSuccess)
                return budgetResult.Error;

            var newVersion = budgetResult.Value;

            string? categoryName = null;
            if (newVersion.CategoryId is not null)
            {
                var category = await _categoryRepository.GetByIdAsync(newVersion.CategoryId, userId, cancellationToken);
                if (category is null)
                    return CategoryErrors.NotFound;

                categoryName = category.Name.Value;
            }

            var versions = await _budgetRepository.GetVersionsAsync(userId, newVersion.Type, newVersion.CategoryId, cancellationToken);
            var change = BudgetTimeline.SetFrom(versions, newVersion);

            await _budgetRepository.ApplyChangeAsync(change, cancellationToken);

            var inForce = change.Added ?? change.Updated.Single(version => version.Covers(from));

            return ToResponseDto(inForce, categoryName);
        }

        /// <summary>Removes the goal <paramref name="id"/> belongs to from <see cref="BudgetMonthQueryDto.Month"/> on.</summary>
        public async Task<Result> RemoveAsync(int id, BudgetMonthQueryDto query, UserId userId, CancellationToken cancellationToken)
        {
            var budget = await _budgetRepository.GetByIdAsync(new BudgetId(id), userId, cancellationToken);
            if (budget is null)
                return BudgetErrors.NotFound;

            if (!YearMonth.TryParse(query.Month, out var from))
                return BudgetErrors.InvalidMonth;

            var versions = await _budgetRepository.GetVersionsAsync(userId, budget.Type, budget.CategoryId, cancellationToken);

            var changeResult = BudgetTimeline.RemoveFrom(versions, from);
            if (!changeResult.IsSuccess)
                return changeResult.Error;

            await _budgetRepository.ApplyChangeAsync(changeResult.Value, cancellationToken);

            return Result.Success();
        }

        private static BudgetGroupDto ToGroupDto(MoneyFlowType type, IReadOnlyList<BudgetListItem> budgets, IReadOnlyList<DashboardCategoryMonthTotal> totals)
        {
            var typeTotals = totals.Where(total => total.Type == type).ToList();
            var typeBudgets = budgets.Where(item => item.Budget.Type == type).ToList();

            var total = typeBudgets.SingleOrDefault(item => item.Budget.CategoryId is null);
            var actual = typeTotals.Sum(row => row.Amount);

            var categories = typeBudgets
                .Where(item => item.Budget.CategoryId is not null)
                .OrderBy(item => item.CategoryName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Budget.CategoryId!.Value)
                .Select(item => ToProgressDto(item, ActualOf(typeTotals, item.Budget.CategoryId!)))
                .ToList();

            return new BudgetGroupDto(actual, total is null ? null : ToProgressDto(total, actual), categories);
        }

        private static decimal ActualOf(IEnumerable<DashboardCategoryMonthTotal> totals, CategoryId categoryId)
            => totals.Where(row => row.CategoryId == categoryId.Value).Sum(row => row.Amount);

        private static BudgetProgressDto ToProgressDto(BudgetListItem item, decimal actual)
        {
            var budget = item.Budget;

            return new BudgetProgressDto(
                budget.Id!.Value,
                budget.CategoryId?.Value,
                item.CategoryName,
                budget.Amount.Value,
                actual,
                budget.Amount.Value - actual,
                budget.ProgressOf(actual),
                budget.IsMetBy(actual),
                budget.FromMonth.ToString(),
                budget.ToMonth?.ToString());
        }

        private static BudgetResponseDto ToResponseDto(Budget budget, string? categoryName)
            => new(
                budget.Id!.Value,
                budget.Type,
                budget.CategoryId?.Value,
                categoryName,
                budget.Amount.Value,
                budget.FromMonth.ToString(),
                budget.ToMonth?.ToString());
    }
}
