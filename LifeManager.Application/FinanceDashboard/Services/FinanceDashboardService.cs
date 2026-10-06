using LifeManager.Application.FinanceDashboard.DTOs;
using LifeManager.Domain.Budgets;
using LifeManager.Domain.Budgets.Interfaces;
using LifeManager.Domain.FinanceDashboard;
using LifeManager.Domain.FinanceDashboard.Interfaces;
using LifeManager.Domain.FinanceDashboard.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.FinanceDashboard.Services
{
    public class FinanceDashboardService(IFinanceDashboardRepository financeDashboardRepository, IBudgetRepository budgetRepository)
    {
        /// <summary>How many categories each breakdown lists before grouping the rest into "others".</summary>
        public const int TopCategoryCount = 8;

        /// <summary>How many categories each goal list shows.</summary>
        public const int TopBudgetCategoryCount = 5;

        private readonly IFinanceDashboardRepository _financeDashboardRepository = financeDashboardRepository;
        private readonly IBudgetRepository _budgetRepository = budgetRepository;

        public async Task<Result<FinanceDashboardResponseDto>> GetAsync(FinanceDashboardQueryDto query, UserId userId, CancellationToken cancellationToken)
        {
            var periodResult = DashboardPeriod.Create(query.From, query.To);
            if (!periodResult.IsSuccess)
                return periodResult.Error;

            var period = periodResult.Value;

            var comparisonResult = period.ComparisonFor(query.Comparison);
            if (!comparisonResult.IsSuccess)
                return comparisonResult.Error;

            var comparison = comparisonResult.Value;

            // One read covers both periods: the comparison always ends before the period starts. With
            // SamePeriodLastYear the months in between are read too and simply belong to neither.
            var rows = await _financeDashboardRepository.GetCategoryMonthTotalsAsync(userId, comparison.FirstDay, period.LastDay, cancellationToken);

            var current = rows.Where(row => period.Contains(row.Year, row.Month)).ToList();
            var previous = rows.Where(row => comparison.Contains(row.Year, row.Month)).ToList();
            var months = period.Months();

            var budgets = await _budgetRepository.GetOverlappingAsync(userId, period.From, period.To, cancellationToken);

            return new FinanceDashboardResponseDto(
                ToPeriodDto(period),
                ToPeriodDto(comparison),
                ToTotalsDto(SumByType(current), SumByType(previous)),
                [.. months.Select(month => ToMonthDto(month, current))],
                ToBreakdownDto(MoneyFlowType.Expense, current, previous, months),
                ToBreakdownDto(MoneyFlowType.Investment, current, previous, months),
                ToBudgetsDto(budgets, current, months));
        }

        /// <summary>
        /// Evaluates every goal month by month (the version in force in each one) against that month's actual amount,
        /// then sums the months that had the goal. The actual amounts are the same rows the rest of the dashboard uses.
        /// </summary>
        private static DashboardBudgetsDto ToBudgetsDto(
            IReadOnlyList<BudgetListItem> budgets,
            IReadOnlyCollection<DashboardCategoryMonthTotal> current,
            IReadOnlyList<YearMonth> months)
        {
            var expenseTotals = MonthlyGoals(budgets, MoneyFlowType.Expense, null, current, months);
            var investmentTotals = MonthlyGoals(budgets, MoneyFlowType.Investment, null, current, months);

            return new DashboardBudgetsDto(
                budgets.Count > 0,
                [.. months.Select((month, index) => new DashboardBudgetMonthDto(
                    month.Year,
                    month.Month,
                    expenseTotals[index]?.Budget.Amount.Value,
                    expenseTotals[index]?.Achieved,
                    investmentTotals[index]?.Budget.Amount.Value,
                    investmentTotals[index]?.Achieved))],
                ToBudgetSummaryDto(expenseTotals),
                ToBudgetSummaryDto(investmentTotals),
                ToBudgetCategoriesDto(budgets, MoneyFlowType.Expense, current, months),
                ToBudgetCategoriesDto(budgets, MoneyFlowType.Investment, current, months));
        }

        /// <summary>For each month of the period, the goal in force (type and category, or the total when null) and how it went; null when there was none.</summary>
        private static IReadOnlyList<MonthGoal?> MonthlyGoals(
            IEnumerable<BudgetListItem> budgets,
            MoneyFlowType type,
            int? categoryId,
            IReadOnlyCollection<DashboardCategoryMonthTotal> current,
            IReadOnlyList<YearMonth> months)
        {
            var versions = budgets
                .Select(item => item.Budget)
                .Where(budget => budget.Type == type && budget.CategoryId?.Value == categoryId)
                .ToList();

            return [.. months.Select(month =>
            {
                var budget = BudgetTimeline.GoalIn(versions, month);
                if (budget is null)
                    return null;

                var actual = current
                    .Where(row => row.Type == type
                        && row.Year == month.Year
                        && row.Month == month.Month
                        && (categoryId is null || row.CategoryId == categoryId))
                    .Sum(row => row.Amount);

                return new MonthGoal(budget, actual, budget.IsMetBy(actual), budget.GapFor(actual));
            })];
        }

        private static DashboardBudgetSummaryDto ToBudgetSummaryDto(IReadOnlyList<MonthGoal?> monthlyGoals)
        {
            var withGoal = monthlyGoals.OfType<MonthGoal>().ToList();
            var goal = withGoal.Sum(monthGoal => monthGoal.Budget.Amount.Value);
            var actual = withGoal.Sum(monthGoal => monthGoal.Actual);

            return new DashboardBudgetSummaryDto(
                goal,
                actual,
                goal == 0 ? null : Math.Round(actual / goal, 4),
                withGoal.Count,
                withGoal.Count(monthGoal => monthGoal.Achieved));
        }

        /// <summary>The type's category goals in the period, furthest from the goal first (ties by name, then id).</summary>
        private static IReadOnlyList<DashboardBudgetCategoryDto> ToBudgetCategoriesDto(
            IReadOnlyList<BudgetListItem> budgets,
            MoneyFlowType type,
            IReadOnlyCollection<DashboardCategoryMonthTotal> current,
            IReadOnlyList<YearMonth> months)
        {
            return [.. budgets
                .Where(item => item.Budget.Type == type && item.Budget.CategoryId is not null)
                .GroupBy(item => item.Budget.CategoryId!.Value)
                .Select(group =>
                {
                    var withGoal = MonthlyGoals(group, type, group.Key, current, months).OfType<MonthGoal>().ToList();
                    var goal = withGoal.Sum(monthGoal => monthGoal.Budget.Amount.Value);
                    var actual = withGoal.Sum(monthGoal => monthGoal.Actual);

                    return new DashboardBudgetCategoryDto(
                        group.Key,
                        group.First().CategoryName ?? string.Empty,
                        goal,
                        actual,
                        Math.Round(actual / goal, 4),
                        withGoal.Sum(monthGoal => monthGoal.Gap),
                        withGoal.Count,
                        withGoal.Count(monthGoal => monthGoal.Achieved));
                })
                .OrderByDescending(category => category.Gap)
                .ThenBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(category => category.CategoryId)
                .Take(TopBudgetCategoryCount)];
        }

        private record MonthGoal(Budget Budget, decimal Actual, bool Achieved, decimal Gap);

        private static DashboardTotals SumByType(IReadOnlyCollection<DashboardCategoryMonthTotal> rows)
            => new(SumOf(rows, MoneyFlowType.Income), SumOf(rows, MoneyFlowType.Expense), SumOf(rows, MoneyFlowType.Investment));

        private static decimal SumOf(IEnumerable<DashboardCategoryMonthTotal> rows, MoneyFlowType type)
            => rows.Where(row => row.Type == type).Sum(row => row.Amount);

        private static DashboardTotalsDto ToTotalsDto(DashboardTotals current, DashboardTotals previous)
            => new(
                ToAmountDto(current.Income, previous.Income),
                ToAmountDto(current.Expense, previous.Expense),
                ToAmountDto(current.Investment, previous.Investment),
                ToAmountDto(current.Balance, previous.Balance));

        private static DashboardAmountDto ToAmountDto(decimal current, decimal previous)
            => new(current, previous, current - previous, DashboardTotals.ChangeRatio(current, previous));

        private static DashboardMonthDto ToMonthDto(YearMonth month, IEnumerable<DashboardCategoryMonthTotal> current)
        {
            var totals = SumByType([.. current.Where(row => row.Year == month.Year && row.Month == month.Month)]);

            return new DashboardMonthDto(month.Year, month.Month, totals.Income, totals.Expense, totals.Investment, totals.Balance);
        }

        /// <summary>
        /// The type's categories in the period, biggest first (ties by name, then id), the top ones listed and the
        /// rest summed into "others". Others' previous amount is whatever the listed ones don't account for, so
        /// it also holds categories that only had amounts in the comparison period, and the parts add up to the totals.
        /// </summary>
        private static DashboardCategoryBreakdownDto ToBreakdownDto(
            MoneyFlowType type,
            IEnumerable<DashboardCategoryMonthTotal> current,
            IEnumerable<DashboardCategoryMonthTotal> previous,
            IReadOnlyList<YearMonth> months)
        {
            var currentRows = current.Where(row => row.Type == type).ToList();
            var previousByCategory = previous
                .Where(row => row.Type == type)
                .GroupBy(row => row.CategoryId)
                .ToDictionary(group => group.Key, group => group.Sum(row => row.Amount));

            var total = currentRows.Sum(row => row.Amount);
            var previousTotal = previousByCategory.Values.Sum();

            var categories = currentRows
                .GroupBy(row => row.CategoryId)
                .Select(group => new
                {
                    CategoryId = group.Key,
                    group.First().CategoryName,
                    Amount = group.Sum(row => row.Amount),
                    MonthlyAmounts = MonthlyAmounts(group, months)
                })
                .OrderByDescending(category => category.Amount)
                .ThenBy(category => category.CategoryName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(category => category.CategoryId)
                .ToList();

            var listed = categories.Take(TopCategoryCount).ToList();
            var rest = categories.Skip(TopCategoryCount).ToList();

            var items = listed.Select(category =>
            {
                var previousAmount = previousByCategory.GetValueOrDefault(category.CategoryId);

                return new DashboardCategoryDto(
                    category.CategoryId,
                    category.CategoryName,
                    category.Amount,
                    previousAmount,
                    DashboardTotals.ChangeRatio(category.Amount, previousAmount),
                    DashboardTotals.Share(category.Amount, total),
                    category.MonthlyAmounts);
            }).ToList();

            DashboardOthersDto? others = null;
            if (rest.Count > 0)
            {
                var othersAmount = rest.Sum(category => category.Amount);
                var othersPrevious = previousTotal - items.Sum(item => item.PreviousAmount);

                others = new DashboardOthersDto(
                    rest.Count,
                    othersAmount,
                    othersPrevious,
                    DashboardTotals.ChangeRatio(othersAmount, othersPrevious),
                    DashboardTotals.Share(othersAmount, total),
                    [.. months.Select((_, index) => rest.Sum(category => category.MonthlyAmounts[index]))]);
            }

            return new DashboardCategoryBreakdownDto(total, previousTotal, DashboardTotals.ChangeRatio(total, previousTotal), items, others);
        }

        private static IReadOnlyList<decimal> MonthlyAmounts(IEnumerable<DashboardCategoryMonthTotal> rows, IReadOnlyList<YearMonth> months)
            => [.. months.Select(month => rows.Where(row => row.Year == month.Year && row.Month == month.Month).Sum(row => row.Amount))];

        private static DashboardPeriodDto ToPeriodDto(DashboardPeriod period)
            => new(period.From.ToString(), period.To.ToString(), period.MonthCount);
    }
}
