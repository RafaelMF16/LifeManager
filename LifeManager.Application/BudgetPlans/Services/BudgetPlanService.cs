using LifeManager.Application.BudgetPlans.DTOs;
using LifeManager.Domain.BudgetPlans;
using LifeManager.Domain.BudgetPlans.Errors;
using LifeManager.Domain.BudgetPlans.Interfaces;
using LifeManager.Domain.FinanceDashboard;
using LifeManager.Domain.FinanceDashboard.Interfaces;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.BudgetPlans.Services
{
    /// <remarks>
    /// Each month has at most one plan and is read, saved and deleted on its own. The actual amounts come from the same
    /// per-category monthly sums the dashboard reads.
    /// </remarks>
    public class BudgetPlanService(
        IBudgetPlanRepository budgetPlanRepository,
        IFinanceDashboardRepository financeDashboardRepository)
    {
        private readonly IBudgetPlanRepository _budgetPlanRepository = budgetPlanRepository;
        private readonly IFinanceDashboardRepository _financeDashboardRepository = financeDashboardRepository;

        /// <param name="month">"yyyy-MM".</param>
        public async Task<Result<BudgetPlanResponseDto>> GetAsync(string? month, UserId userId, CancellationToken cancellationToken)
        {
            if (!YearMonth.TryParse(month, out var yearMonth))
                return BudgetPlanErrors.InvalidMonth;

            return await ToResponseDtoAsync(yearMonth, userId, cancellationToken);
        }

        private async Task<BudgetPlanResponseDto> ToResponseDtoAsync(YearMonth month, UserId userId, CancellationToken cancellationToken)
        {
            var item = await _budgetPlanRepository.GetAsync(userId, month, cancellationToken);
            var copySource = await _budgetPlanRepository.GetLatestMonthBeforeAsync(userId, month, cancellationToken);
            var totals = await _financeDashboardRepository.GetCategoryMonthTotalsAsync(userId, month.FirstDay, month.LastDay, cancellationToken);

            var expenses = totals.Where(total => total.Type == MoneyFlowType.Expense).ToList();

            if (item is null)
                return new BudgetPlanResponseDto(month.ToString(), false, null, [], 100m, null, expenses.Sum(row => row.Amount), copySource?.ToString());

            var plan = item.Plan;
            var slicedCategoryIds = plan.Slices
                .Where(slice => slice.CategoryId is not null)
                .Select(slice => slice.CategoryId!.Value)
                .ToHashSet();

            var expenseSlices = plan.Slices
                .Where(slice => slice.Type == MoneyFlowType.Expense)
                .Select(slice => ToSliceDto(plan, slice, item.CategoryNames.GetValueOrDefault(slice.CategoryId!.Value), ActualOf(expenses, slice.CategoryId!.Value)))
                .OrderBy(slice => slice.CategoryName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(slice => slice.CategoryId);

            var investmentSlices = plan.InvestmentSlice is null
                ? []
                : new[] { ToSliceDto(plan, plan.InvestmentSlice, null, totals.Where(total => total.Type == MoneyFlowType.Investment).Sum(row => row.Amount)) };

            return new BudgetPlanResponseDto(
                month.ToString(),
                true,
                plan.Total.Value,
                [.. expenseSlices, .. investmentSlices],
                plan.FreePercent,
                plan.FreeAmount,
                expenses.Where(row => !slicedCategoryIds.Contains(row.CategoryId)).Sum(row => row.Amount),
                copySource?.ToString());
        }

        private static decimal ActualOf(IEnumerable<DashboardCategoryMonthTotal> totals, int categoryId)
            => totals.Where(row => row.CategoryId == categoryId).Sum(row => row.Amount);

        private static BudgetSliceProgressDto ToSliceDto(BudgetPlan plan, BudgetSlice slice, string? categoryName, decimal actual)
            => new(
                slice.Type,
                slice.CategoryId?.Value,
                categoryName,
                slice.Percent.Value,
                slice.AmountOf(plan.Total),
                actual,
                slice.ProgressOf(actual, plan.Total),
                slice.IsMetBy(actual, plan.Total));
    }
}
