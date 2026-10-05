using LifeManager.Application.FinanceDashboard.DTOs;
using LifeManager.Domain.FinanceDashboard;
using LifeManager.Domain.FinanceDashboard.Interfaces;
using LifeManager.Domain.FinanceDashboard.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.FinanceDashboard.Services
{
    public class FinanceDashboardService(IFinanceDashboardRepository financeDashboardRepository)
    {
        /// <summary>How many categories each breakdown lists before grouping the rest into "others".</summary>
        public const int TopCategoryCount = 8;

        private readonly IFinanceDashboardRepository _financeDashboardRepository = financeDashboardRepository;

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

            return new FinanceDashboardResponseDto(
                ToPeriodDto(period),
                ToPeriodDto(comparison),
                ToTotalsDto(SumByType(current), SumByType(previous)),
                [.. months.Select(month => ToMonthDto(month, current))],
                ToBreakdownDto(MoneyFlowType.Expense, current, previous, months),
                ToBreakdownDto(MoneyFlowType.Investment, current, previous, months));
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
