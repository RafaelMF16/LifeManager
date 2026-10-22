using LifeManager.Application.FinanceDashboard.DTOs;
using LifeManager.Domain.BudgetPlans;
using LifeManager.Domain.BudgetPlans.Interfaces;
using LifeManager.Domain.FinanceDashboard;
using LifeManager.Domain.FinanceDashboard.Interfaces;
using LifeManager.Domain.FinanceDashboard.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.FinanceDashboard.Services
{
    public class FinanceDashboardService(IFinanceDashboardRepository financeDashboardRepository, IBudgetPlanRepository budgetPlanRepository)
    {
        /// <summary>How many categories each breakdown lists before grouping the rest into "others".</summary>
        public const int TopCategoryCount = 8;

        /// <summary>How many categories each goal list shows.</summary>
        public const int TopBudgetCategoryCount = 5;

        private readonly IFinanceDashboardRepository _financeDashboardRepository = financeDashboardRepository;
        private readonly IBudgetPlanRepository _budgetPlanRepository = budgetPlanRepository;

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

            var plans = await _budgetPlanRepository.GetOverlappingAsync(userId, period.From, period.To, cancellationToken);

            return new FinanceDashboardResponseDto(
                ToPeriodDto(period),
                ToPeriodDto(comparison),
                ToTotalsDto(SumByType(current), SumByType(previous)),
                [.. months.Select(month => ToMonthDto(month, current))],
                ToBreakdownDto(MoneyFlowType.Expense, current, previous, months),
                ToBreakdownDto(MoneyFlowType.Investment, current, previous, months),
                ToBudgetsDto(plans, current, months));
        }

        /// <summary>
        /// Evaluates each month against its own plan: the spending limit (the total minus the investment slice), the
        /// investment slice and each expense slice, against that month's actual amounts. The actual amounts are the
        /// same rows the rest of the dashboard uses. Investment is a single slice, so there are no investment categories.
        /// </summary>
        private static DashboardBudgetsDto ToBudgetsDto(
            IReadOnlyList<BudgetPlanListItem> plans,
            IReadOnlyCollection<DashboardCategoryMonthTotal> current,
            IReadOnlyList<YearMonth> months)
        {
            var plansByMonth = plans.ToDictionary(item => item.Plan.Month.Ordinal, item => item.Plan);

            var expenseTotals = months
                .Select(month => plansByMonth.GetValueOrDefault(month.Ordinal) is { } plan
                    ? MonthGoal.Limit(plan.SpendingLimit, ActualOf(current, month, MoneyFlowType.Expense, null))
                    : null)
                .ToList();
            var investmentTotals = months
                .Select(month => plansByMonth.GetValueOrDefault(month.Ordinal) is { InvestmentSlice: { } slice } plan
                    ? MonthGoal.Target(slice.AmountOf(plan.Total), ActualOf(current, month, MoneyFlowType.Investment, null))
                    : null)
                .ToList();

            return new DashboardBudgetsDto(
                plans.Count > 0,
                [.. months.Select((month, index) => new DashboardBudgetMonthDto(
                    month.Year,
                    month.Month,
                    expenseTotals[index]?.Goal,
                    expenseTotals[index]?.Achieved,
                    investmentTotals[index]?.Goal,
                    investmentTotals[index]?.Achieved))],
                ToBudgetSummaryDto(expenseTotals),
                ToBudgetSummaryDto(investmentTotals),
                ToBudgetCategoriesDto(plans, current),
                []);
        }

        /// <summary>What the month spent (or invested), in one category or, when null, in all of them.</summary>
        private static decimal ActualOf(IEnumerable<DashboardCategoryMonthTotal> current, YearMonth month, MoneyFlowType type, int? categoryId)
            => current
                .Where(row => row.Type == type
                    && row.Year == month.Year
                    && row.Month == month.Month
                    && (categoryId is null || row.CategoryId == categoryId))
                .Sum(row => row.Amount);

        private static DashboardBudgetSummaryDto ToBudgetSummaryDto(IReadOnlyList<MonthGoal?> monthlyGoals)
        {
            var withGoal = monthlyGoals.OfType<MonthGoal>().ToList();
            var goal = withGoal.Sum(monthGoal => monthGoal.Goal);
            var actual = withGoal.Sum(monthGoal => monthGoal.Actual);

            return new DashboardBudgetSummaryDto(
                goal,
                actual,
                goal == 0 ? null : Math.Round(actual / goal, 4),
                withGoal.Count,
                withGoal.Count(monthGoal => monthGoal.Achieved));
        }

        /// <summary>The expense slices of the period's plans by category, furthest from their limit first (ties by name, then id).</summary>
        private static IReadOnlyList<DashboardBudgetCategoryDto> ToBudgetCategoriesDto(
            IReadOnlyList<BudgetPlanListItem> plans,
            IReadOnlyCollection<DashboardCategoryMonthTotal> current)
        {
            return [.. plans
                .SelectMany(item => item.Plan.Slices
                    .Where(slice => slice.Type == MoneyFlowType.Expense)
                    .Select(slice => (item, slice, categoryId: slice.CategoryId!.Value)))
                .GroupBy(entry => entry.categoryId)
                .Select(group =>
                {
                    var withGoal = group
                        .Select(entry => MonthGoal.Limit(
                            entry.slice.AmountOf(entry.item.Plan.Total),
                            ActualOf(current, entry.item.Plan.Month, MoneyFlowType.Expense, group.Key)))
                        .ToList();
                    var goal = withGoal.Sum(monthGoal => monthGoal.Goal);
                    var actual = withGoal.Sum(monthGoal => monthGoal.Actual);

                    return new DashboardBudgetCategoryDto(
                        group.Key,
                        group.First().item.CategoryNames.GetValueOrDefault(group.Key) ?? string.Empty,
                        goal,
                        actual,
                        // A slice can round to no cents (a tiny share of a tiny total); it reads as no progress.
                        goal == 0 ? 0 : Math.Round(actual / goal, 4),
                        withGoal.Sum(monthGoal => monthGoal.Gap),
                        withGoal.Count,
                        withGoal.Count(monthGoal => monthGoal.Achieved));
                })
                .OrderByDescending(category => category.Gap)
                .ThenBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(category => category.CategoryId)
                .Take(TopBudgetCategoryCount)];
        }

        /// <summary>One month's goal and how it went. <see cref="Gap"/> is what was spent over a limit or is missing to a target.</summary>
        private record MonthGoal(decimal Goal, decimal Actual, bool Achieved, decimal Gap)
        {
            public static MonthGoal Limit(decimal goal, decimal actual) => new(goal, actual, actual <= goal, Math.Max(0, actual - goal));

            public static MonthGoal Target(decimal goal, decimal actual) => new(goal, actual, actual >= goal, Math.Max(0, goal - actual));
        }

        private static DashboardTotals SumByType(IReadOnlyCollection<DashboardCategoryMonthTotal> rows)
            => new(SumOf(rows, MoneyFlowType.Income), SumOf(rows, MoneyFlowType.Expense), SumOf(rows, MoneyFlowType.Investment));

        private static decimal SumOf(IEnumerable<DashboardCategoryMonthTotal> rows, MoneyFlowType type)
            => rows.Where(row => row.Type == type).Sum(row => row.Amount);

        private static DashboardTotalsDto ToTotalsDto(DashboardTotals current, DashboardTotals previous)
            => new(
                ToAmountDto(current.Income, previous.Income),
                ToAmountDto(current.Expense, previous.Expense),
                ToAmountDto(current.Investment, previous.Investment),
                ToAmountDto(current.Balance, previous.Balance),
                ToAmountDto(current.Saved, previous.Saved),
                current.SavingsRate);

        private static DashboardAmountDto ToAmountDto(decimal current, decimal previous)
            => new(current, previous, current - previous, DashboardTotals.ChangeRatio(current, previous));

        private static DashboardMonthDto ToMonthDto(YearMonth month, IEnumerable<DashboardCategoryMonthTotal> current)
        {
            var totals = SumByType([.. current.Where(row => row.Year == month.Year && row.Month == month.Month)]);

            return new DashboardMonthDto(month.Year, month.Month, totals.Income, totals.Expense, totals.Investment, totals.Balance, totals.Saved);
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
