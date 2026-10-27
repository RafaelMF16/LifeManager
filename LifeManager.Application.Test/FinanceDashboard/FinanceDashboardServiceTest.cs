using LifeManager.Application.FinanceDashboard.DTOs;
using LifeManager.Application.FinanceDashboard.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.BudgetPlans;
using LifeManager.Domain.Categories;
using LifeManager.Domain.FinanceDashboard.Enums;
using LifeManager.Domain.FinanceDashboard.Errors;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.FinanceDashboard
{
    [Collection("ApplicationServices")]
    public class FinanceDashboardServiceTest : BaseTest
    {
        private static readonly UserId FirstUserId = new(1);
        private static readonly UserId SecondUserId = new(2);

        // Categories 1-4 belong to the first user, 5 to the second.
        private const int MarketCategoryId = 1;
        private const int SalaryCategoryId = 2;
        private const int RentCategoryId = 3;
        private const int TreasuryCategoryId = 4;
        private const int OtherUserCategoryId = 5;

        private readonly FinanceDashboardService _financeDashboardService;

        private int _nextMonthId = 1;
        private int _nextTransactionId = 1;
        private int _nextPlanId = 1;

        public FinanceDashboardServiceTest()
        {
            _financeDashboardService = ServiceProvider.GetRequiredService<FinanceDashboardService>();

            TransactionSingleton.Instance.Clear();
            MonthlySummarySingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();
            BudgetPlanSingleton.Instance.Clear();

            SeedCategory(MarketCategoryId, FirstUserId, "Mercado");
            SeedCategory(SalaryCategoryId, FirstUserId, "Salário");
            SeedCategory(RentCategoryId, FirstUserId, "Aluguel");
            SeedCategory(TreasuryCategoryId, FirstUserId, "Tesouro");
            SeedCategory(OtherUserCategoryId, SecondUserId, "Outro");
        }

        [Fact]
        public async Task GetAsync_ShouldZeroFillMonths_WhenPeriodHasNoTransactions()
        {
            var result = await Get("2026-04", "2026-06");

            Assert.True(result.IsSuccess);
            Assert.Equal(new DashboardPeriodDto("2026-04", "2026-06", 3), result.Value.Period);
            Assert.Equal(
                [new DashboardMonthDto(2026, 4, 0, 0, 0, 0, 0), new DashboardMonthDto(2026, 5, 0, 0, 0, 0, 0), new DashboardMonthDto(2026, 6, 0, 0, 0, 0, 0)],
                result.Value.Months);
            Assert.Equal(new DashboardAmountDto(0, 0, 0, null), result.Value.Totals.Income);
            Assert.Empty(result.Value.Expenses.Items);
            Assert.Null(result.Value.Expenses.Others);
            Assert.Equal(0, result.Value.Investments.Total);
        }

        [Fact]
        public async Task GetAsync_ShouldSumTotalsByType()
        {
            var april = SeedMonth(FirstUserId, 2026, 4);
            SeedTransaction(april, MoneyFlowType.Income, SalaryCategoryId, 5000m);
            SeedTransaction(april, MoneyFlowType.Expense, MarketCategoryId, 600m);
            SeedTransaction(april, MoneyFlowType.Expense, RentCategoryId, 900m);
            SeedTransaction(april, MoneyFlowType.Investment, TreasuryCategoryId, 1000m);

            var result = await Get("2026-04", "2026-04");

            Assert.True(result.IsSuccess);
            var totals = result.Value.Totals;
            Assert.Equal(5000m, totals.Income.Current);
            Assert.Equal(1500m, totals.Expense.Current);
            Assert.Equal(1000m, totals.Investment.Current);
            Assert.Equal(2500m, totals.Balance.Current);
            Assert.Equal(3500m, totals.Saved.Current);
            Assert.Equal(0.7m, totals.SavingsRate);
            Assert.Equal([new DashboardMonthDto(2026, 4, 5000m, 1500m, 1000m, 2500m, 3500m)], result.Value.Months);
            Assert.Equal(1500m, result.Value.Expenses.Total);
            Assert.Equal(1000m, result.Value.Investments.Total);
            Assert.Equal("Tesouro", Assert.Single(result.Value.Investments.Items).Name);
        }

        [Fact]
        public async Task GetAsync_ShouldIgnoreOtherUsersTransactions()
        {
            var otherUserApril = SeedMonth(SecondUserId, 2026, 4);
            SeedTransaction(otherUserApril, MoneyFlowType.Expense, OtherUserCategoryId, 999m);

            var result = await Get("2026-04", "2026-04");

            Assert.True(result.IsSuccess);
            Assert.Equal(0m, result.Value.Totals.Expense.Current);
            Assert.Empty(result.Value.Expenses.Items);
        }

        [Fact]
        public async Task GetAsync_ShouldComparePreviousEquivalentPeriod_AndIgnoreMonthsOutsideBoth()
        {
            var january = SeedMonth(FirstUserId, 2026, 1);
            var march = SeedMonth(FirstUserId, 2026, 3);
            var april = SeedMonth(FirstUserId, 2026, 4);
            var may = SeedMonth(FirstUserId, 2026, 5);
            var june = SeedMonth(FirstUserId, 2026, 6);
            SeedTransaction(january, MoneyFlowType.Expense, MarketCategoryId, 999m);     // before the comparison period (Feb–Mar)
            SeedTransaction(march, MoneyFlowType.Expense, MarketCategoryId, 200m);       // comparison period
            SeedTransaction(april, MoneyFlowType.Expense, MarketCategoryId, 100m);
            SeedTransaction(may, MoneyFlowType.Expense, MarketCategoryId, 200m);
            SeedTransaction(june, MoneyFlowType.Expense, MarketCategoryId, 999m);        // after the period

            var result = await Get("2026-04", "2026-05");

            Assert.True(result.IsSuccess);
            Assert.Equal(new DashboardPeriodDto("2026-02", "2026-03", 2), result.Value.ComparisonPeriod);
            Assert.Equal(new DashboardAmountDto(300m, 200m, 100m, 0.5m), result.Value.Totals.Expense);
            var market = Assert.Single(result.Value.Expenses.Items);
            Assert.Equal(200m, market.PreviousAmount);
            Assert.Equal(0.5m, market.ChangeRatio);
        }

        [Fact]
        public async Task GetAsync_ShouldCompareSameMonthsLastYear_WhenComparisonIsSamePeriodLastYear()
        {
            SeedTransaction(SeedMonth(FirstUserId, 2025, 2), MoneyFlowType.Income, SalaryCategoryId, 4000m);   // same months last year
            SeedTransaction(SeedMonth(FirstUserId, 2025, 6), MoneyFlowType.Income, SalaryCategoryId, 999m);    // between both periods
            SeedTransaction(SeedMonth(FirstUserId, 2026, 2), MoneyFlowType.Income, SalaryCategoryId, 5000m);

            var result = await Get("2026-01", "2026-03", DashboardComparison.SamePeriodLastYear);

            Assert.True(result.IsSuccess);
            Assert.Equal(new DashboardPeriodDto("2025-01", "2025-03", 3), result.Value.ComparisonPeriod);
            Assert.Equal(new DashboardAmountDto(5000m, 4000m, 1000m, 0.25m), result.Value.Totals.Income);
        }

        [Fact]
        public async Task GetAsync_ShouldReturnNullChange_WhenPreviousIsZero()
        {
            SeedTransaction(SeedMonth(FirstUserId, 2026, 4), MoneyFlowType.Investment, TreasuryCategoryId, 500m);

            var result = await Get("2026-04", "2026-04");

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Totals.Investment.ChangeRatio);
            Assert.Null(result.Value.Investments.ChangeRatio);
            Assert.Null(Assert.Single(result.Value.Investments.Items).ChangeRatio);
        }

        [Fact]
        public async Task GetAsync_ShouldCompareBalanceAgainstItsSize_WhenPreviousBalanceIsNegative()
        {
            SeedTransaction(SeedMonth(FirstUserId, 2026, 3), MoneyFlowType.Expense, MarketCategoryId, 100m);   // balance −100
            SeedTransaction(SeedMonth(FirstUserId, 2026, 4), MoneyFlowType.Expense, MarketCategoryId, 50m);    // balance −50

            var result = await Get("2026-04", "2026-04");

            Assert.Equal(new DashboardAmountDto(-50m, -100m, 50m, 0.5m), result.Value!.Totals.Balance);
        }

        [Fact]
        public async Task GetAsync_ShouldCompareSaved_WithoutCountingInvestmentAsSpending()
        {
            var march = SeedMonth(FirstUserId, 2026, 3);
            var april = SeedMonth(FirstUserId, 2026, 4);
            SeedTransaction(march, MoneyFlowType.Income, SalaryCategoryId, 4000m);
            SeedTransaction(march, MoneyFlowType.Expense, MarketCategoryId, 3000m);      // saved 1000
            SeedTransaction(april, MoneyFlowType.Income, SalaryCategoryId, 5000m);
            SeedTransaction(april, MoneyFlowType.Expense, MarketCategoryId, 3000m);
            SeedTransaction(april, MoneyFlowType.Investment, TreasuryCategoryId, 2500m); // saved 2000, balance −500

            var result = await Get("2026-04", "2026-04");

            var totals = result.Value!.Totals;
            Assert.Equal(new DashboardAmountDto(2000m, 1000m, 1000m, 1m), totals.Saved);
            Assert.Equal(0.4m, totals.SavingsRate);
            Assert.Equal(-500m, totals.Balance.Current);
            Assert.Equal([new DashboardMonthDto(2026, 4, 5000m, 3000m, 2500m, -500m, 2000m)], result.Value.Months);
        }

        [Fact]
        public async Task GetAsync_ShouldLeaveSavingsRateEmpty_WhenPeriodHasNoIncome()
        {
            SeedTransaction(SeedMonth(FirstUserId, 2026, 4), MoneyFlowType.Expense, MarketCategoryId, 50m);

            var result = await Get("2026-04", "2026-04");

            Assert.Equal(-50m, result.Value!.Totals.Saved.Current);
            Assert.Null(result.Value.Totals.SavingsRate);
        }

        [Fact]
        public async Task GetAsync_ShouldGroupCategoriesBeyondTheTopOnesIntoOthers()
        {
            var march = SeedMonth(FirstUserId, 2026, 3);
            var april = SeedMonth(FirstUserId, 2026, 4);
            for (var index = 1; index <= 10; index++)
            {
                var categoryId = 100 + index;
                SeedCategory(categoryId, FirstUserId, $"Categoria {index:D2}");
                SeedTransaction(april, MoneyFlowType.Expense, categoryId, index * 100m);
            }
            SeedCategory(200, FirstUserId, "Só no mês anterior");
            SeedTransaction(march, MoneyFlowType.Expense, 200, 70m);
            SeedTransaction(march, MoneyFlowType.Expense, 110, 1000m);

            var result = await Get("2026-04", "2026-04");

            Assert.True(result.IsSuccess);
            var expenses = result.Value.Expenses;
            Assert.Equal(5500m, expenses.Total);
            Assert.Equal(1070m, expenses.PreviousTotal);
            Assert.Equal(FinanceDashboardService.TopCategoryCount, expenses.Items.Count);
            Assert.Equal([110, 109, 108, 107, 106, 105, 104, 103], expenses.Items.Select(item => item.CategoryId));
            Assert.Equal(0.1818m, expenses.Items[0].Share);
            // Others: categories 01 and 02 now; previous is what the listed ones don't explain (the 70 of a category gone this month).
            var others = Assert.IsType<DashboardOthersDto>(expenses.Others);
            Assert.Equal(2, others.CategoryCount);
            Assert.Equal(300m, others.Amount);
            Assert.Equal(70m, others.PreviousAmount);
            Assert.Equal(3.2857m, others.ChangeRatio);
            Assert.Equal(0.0545m, others.Share);
            Assert.Equal([300m], others.MonthlyAmounts);
        }

        [Fact]
        public async Task GetAsync_ShouldRankEqualAmountsByName()
        {
            var april = SeedMonth(FirstUserId, 2026, 4);
            SeedTransaction(april, MoneyFlowType.Expense, MarketCategoryId, 100m);   // Mercado
            SeedTransaction(april, MoneyFlowType.Expense, RentCategoryId, 100m);     // Aluguel

            var result = await Get("2026-04", "2026-04");

            Assert.Equal(["Aluguel", "Mercado"], result.Value!.Expenses.Items.Select(item => item.Name));
        }

        [Fact]
        public async Task GetAsync_ShouldAlignMonthlyAmountsWithMonths()
        {
            SeedTransaction(SeedMonth(FirstUserId, 2026, 4), MoneyFlowType.Expense, MarketCategoryId, 10m);
            var june = SeedMonth(FirstUserId, 2026, 6);
            SeedTransaction(june, MoneyFlowType.Expense, MarketCategoryId, 20m);
            SeedTransaction(june, MoneyFlowType.Expense, MarketCategoryId, 10m, day: 30);

            var result = await Get("2026-04", "2026-06");

            Assert.True(result.IsSuccess);
            Assert.Equal([10m, 0m, 30m], Assert.Single(result.Value.Expenses.Items).MonthlyAmounts);
            Assert.Equal([10m, 0m, 30m], result.Value.Months.Select(month => month.Expense));
        }

        [Theory]
        [InlineData("2026-13", "2026-12", DashboardComparison.PreviousPeriod, "FinanceDashboard.InvalidFrom")]
        [InlineData(null, "2026-12", DashboardComparison.PreviousPeriod, "FinanceDashboard.InvalidFrom")]
        [InlineData("2026-01", "", DashboardComparison.PreviousPeriod, "FinanceDashboard.InvalidTo")]
        [InlineData("2026-05", "2026-04", DashboardComparison.PreviousPeriod, "FinanceDashboard.EndBeforeStart")]
        [InlineData("2023-01", "2026-12", DashboardComparison.PreviousPeriod, "FinanceDashboard.PeriodTooLong")]
        [InlineData("2025-01", "2026-01", DashboardComparison.SamePeriodLastYear, "FinanceDashboard.ComparisonTooLong")]
        [InlineData("2026-01", "2026-02", (DashboardComparison)99, "FinanceDashboard.InvalidComparison")]
        public async Task GetAsync_ShouldReturnValidationError_WhenQueryIsInvalid(string? from, string? to, DashboardComparison comparison, string expectedCode)
        {
            var result = await Get(from, to, comparison);

            Assert.False(result.IsSuccess);
            Assert.Equal(expectedCode, result.Error.Code);
            Assert.Equal(ErrorType.Validation, result.Error.Type);
        }

        [Fact]
        public void Errors_ShouldAllBeValidationErrors()
        {
            Assert.All(
                [FinanceDashboardErrors.InvalidFrom, FinanceDashboardErrors.InvalidTo, FinanceDashboardErrors.EndBeforeStart,
                    FinanceDashboardErrors.PeriodTooLong, FinanceDashboardErrors.InvalidComparison, FinanceDashboardErrors.ComparisonTooLong],
                error => Assert.Equal(ErrorType.Validation, error.Type));
        }

        [Fact]
        public async Task GetAsync_ShouldReturnEmptyBudgets_WhenThereAreNoGoals()
        {
            var result = await Get("2026-04", "2026-05");

            var budgets = result.Value!.Budgets;
            Assert.False(budgets.HasGoals);
            Assert.Equal(
                [new DashboardBudgetMonthDto(2026, 4, null, null, null, null), new DashboardBudgetMonthDto(2026, 5, null, null, null, null)],
                budgets.Months);
            Assert.Equal(new DashboardBudgetSummaryDto(0, 0, null, 0, 0), budgets.Expense);
            Assert.Empty(budgets.ExpenseCategories);
            Assert.Empty(budgets.InvestmentCategories);
        }

        [Fact]
        public async Task GetAsync_ShouldEvaluateTheTotalGoals_MonthByMonth_FromEachMonthsPlan()
        {
            var april = SeedMonth(FirstUserId, 2026, 4);
            var may = SeedMonth(FirstUserId, 2026, 5);
            var july = SeedMonth(FirstUserId, 2026, 7);
            SeedTransaction(april, MoneyFlowType.Expense, MarketCategoryId, 900m);
            SeedTransaction(may, MoneyFlowType.Expense, RentCategoryId, 1700m);
            SeedTransaction(april, MoneyFlowType.Investment, TreasuryCategoryId, 500m);
            SeedTransaction(july, MoneyFlowType.Expense, RentCategoryId, 9999m);
            SeedPlan("2026-04", 2000m, Investment(20m));
            SeedPlan("2026-05", 2000m, Investment(20m));
            SeedPlan("2026-06", 1000m);
            SeedPlan("2026-07", 1000m);

            var result = await Get("2026-04", "2026-06");

            var budgets = result.Value!.Budgets;
            Assert.True(budgets.HasGoals);
            Assert.Equal(
                [
                    // The spending limit is the total minus the investment slice, the free part included.
                    new DashboardBudgetMonthDto(2026, 4, 1600m, true, 400m, true),
                    new DashboardBudgetMonthDto(2026, 5, 1600m, false, 400m, false),
                    new DashboardBudgetMonthDto(2026, 6, 1000m, true, null, null)
                ],
                budgets.Months);
            Assert.Equal(new DashboardBudgetSummaryDto(4200m, 2600m, 0.619m, 3, 2), budgets.Expense);
            Assert.Equal(new DashboardBudgetSummaryDto(800m, 500m, 0.625m, 2, 1), budgets.Investment);
        }

        [Fact]
        public async Task GetAsync_ShouldEvaluateEachCategory_WithTheSliceOfEachMonth()
        {
            var april = SeedMonth(FirstUserId, 2026, 4);
            var may = SeedMonth(FirstUserId, 2026, 5);
            SeedTransaction(april, MoneyFlowType.Expense, MarketCategoryId, 700m);
            SeedTransaction(may, MoneyFlowType.Expense, MarketCategoryId, 700m);
            SeedPlan("2026-04", 2000m, Expense(MarketCategoryId, 30m));
            SeedPlan("2026-05", 2000m, Expense(MarketCategoryId, 40m));

            var result = await Get("2026-04", "2026-05");

            var market = Assert.Single(result.Value!.Budgets.ExpenseCategories);
            Assert.Equal(new DashboardBudgetCategoryDto(MarketCategoryId, "Mercado", 1400m, 1400m, 1m, 100m, 2, 1), market);
        }

        [Fact]
        public async Task GetAsync_ShouldListCategoryGoals_MostMissedFirst_EvenBeyondTheTopCategories()
        {
            var april = SeedMonth(FirstUserId, 2026, 4);
            // Nine bigger categories push Rent out of the expense breakdown's top 8, but not out of the goals.
            for (var index = 0; index < 9; index++)
            {
                var categoryId = 100 + index;
                SeedCategory(categoryId, FirstUserId, $"Categoria {index}");
                SeedTransaction(april, MoneyFlowType.Expense, categoryId, 5000m);
            }
            SeedTransaction(april, MoneyFlowType.Expense, RentCategoryId, 1200m);
            SeedTransaction(april, MoneyFlowType.Expense, MarketCategoryId, 500m);
            SeedTransaction(april, MoneyFlowType.Investment, TreasuryCategoryId, 300m);
            SeedPlan("2026-04", 10000m, Expense(RentCategoryId, 10m), Expense(MarketCategoryId, 8m), Investment(10m));

            var result = await Get("2026-04", "2026-04");

            var budgets = result.Value!.Budgets;
            Assert.DoesNotContain(result.Value.Expenses.Items, item => item.CategoryId == RentCategoryId);
            Assert.Equal(["Aluguel", "Mercado"], budgets.ExpenseCategories.Select(category => category.Name).ToArray());
            Assert.Equal(200m, budgets.ExpenseCategories[0].Gap);
            Assert.Equal(0, budgets.ExpenseCategories[0].MonthsAchieved);
            Assert.Equal(1, budgets.ExpenseCategories[1].MonthsAchieved);
        }

        [Fact]
        public async Task GetAsync_ShouldNotListInvestmentCategories_SinceInvestmentIsOneSlice()
        {
            SeedTransaction(SeedMonth(FirstUserId, 2026, 4), MoneyFlowType.Investment, TreasuryCategoryId, 300m);
            SeedPlan("2026-04", 10000m, Investment(10m));

            var result = await Get("2026-04", "2026-04");

            var budgets = result.Value!.Budgets;
            Assert.Empty(budgets.InvestmentCategories);
            Assert.Equal(new DashboardBudgetSummaryDto(1000m, 300m, 0.3m, 1, 0), budgets.Investment);
        }

        [Fact]
        public async Task GetAsync_ShouldIgnoreOtherUsersPlans()
        {
            SeedPlan("2026-04", 1000m, userId: SecondUserId);

            var result = await Get("2026-04", "2026-04");

            Assert.False(result.Value!.Budgets.HasGoals);
        }

        private static BudgetSlice Expense(int categoryId, decimal percent)
            => BudgetSlice.Create(MoneyFlowType.Expense, categoryId, percent).Value!;

        private static BudgetSlice Investment(decimal percent)
            => BudgetSlice.Create(MoneyFlowType.Investment, null, percent).Value!;

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("2026-13")]
        [InlineData("2026-1")]
        [InlineData("1999-12")]
        public async Task GetDaysAsync_ShouldFail_WhenMonthIsInvalid(string? month)
        {
            var result = await GetDays(month);

            Assert.False(result.IsSuccess);
            Assert.Equal(FinanceDashboardErrors.InvalidMonth, result.Error);
        }

        [Theory]
        [InlineData("2024-02", 29)]
        [InlineData("2026-02", 28)]
        [InlineData("2026-04", 30)]
        [InlineData("2026-10", 31)]
        public async Task GetDaysAsync_ShouldReturnEveryDayOfTheMonthInOrder(string month, int dayCount)
        {
            var result = await GetDays(month);

            Assert.True(result.IsSuccess);
            Assert.Equal(month, result.Value.Month);
            Assert.Equal(dayCount, result.Value.Days.Count);
            Assert.Equal(
                Enumerable.Range(1, dayCount).Select(day => $"{month}-{day:D2}"),
                result.Value.Days.Select(day => day.Date.ToString("yyyy-MM-dd")));
        }

        [Fact]
        public async Task GetDaysAsync_ShouldZeroFill_WhenMonthDoesNotExist()
        {
            var result = await GetDays("2026-10");

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.MonthlySummaryId);
            Assert.Equal(0, result.Value.Total);
            Assert.Equal(0, result.Value.MaxDayTotal);
            Assert.All(result.Value.Days, day => Assert.Equal((0m, 0), (day.Total, day.Count)));
        }

        [Fact]
        public async Task GetDaysAsync_ShouldSumExpensesPerDay()
        {
            var october = SeedMonth(FirstUserId, 2026, 10);
            SeedTransaction(october, MoneyFlowType.Expense, MarketCategoryId, 62.40m, day: 2);
            SeedTransaction(october, MoneyFlowType.Expense, RentCategoryId, 25.50m, day: 2);
            SeedTransaction(october, MoneyFlowType.Expense, MarketCategoryId, 640m, day: 15);

            var result = await GetDays("2026-10");

            Assert.True(result.IsSuccess);
            Assert.Equal(october.Id!.Value, result.Value.MonthlySummaryId);
            Assert.Equal(727.90m, result.Value.Total);
            Assert.Equal(640m, result.Value.MaxDayTotal);
            Assert.Equal(new DashboardDayDto(new DateOnly(2026, 10, 2), 87.90m, 2), result.Value.Days[1]);
            Assert.Equal(new DashboardDayDto(new DateOnly(2026, 10, 15), 640m, 1), result.Value.Days[14]);
            Assert.Equal(new DashboardDayDto(new DateOnly(2026, 10, 1), 0, 0), result.Value.Days[0]);
        }

        [Fact]
        public async Task GetDaysAsync_ShouldIgnoreIncomeInvestmentAndOtherUsers()
        {
            var october = SeedMonth(FirstUserId, 2026, 10);
            SeedTransaction(october, MoneyFlowType.Income, SalaryCategoryId, 5000m, day: 5);
            SeedTransaction(october, MoneyFlowType.Investment, TreasuryCategoryId, 1000m, day: 5);
            SeedTransaction(october, MoneyFlowType.Expense, MarketCategoryId, 30m, day: 5);
            var otherUserOctober = SeedMonth(SecondUserId, 2026, 10);
            SeedTransaction(otherUserOctober, MoneyFlowType.Expense, OtherUserCategoryId, 999m, day: 5);

            var result = await GetDays("2026-10");

            Assert.True(result.IsSuccess);
            Assert.Equal(30m, result.Value.Total);
            Assert.Equal(new DashboardDayDto(new DateOnly(2026, 10, 5), 30m, 1), result.Value.Days[4]);
        }

        [Fact]
        public async Task GetDaysAsync_ShouldMatchTheDashboardMonthExpense()
        {
            var october = SeedMonth(FirstUserId, 2026, 10);
            SeedTransaction(october, MoneyFlowType.Expense, MarketCategoryId, 120m, day: 3);
            SeedTransaction(october, MoneyFlowType.Expense, RentCategoryId, 900m, day: 10);
            SeedTransaction(october, MoneyFlowType.Income, SalaryCategoryId, 5000m, day: 10);

            var days = await GetDays("2026-10");
            var dashboard = await Get("2026-10", "2026-10");

            Assert.Equal(dashboard.Value.Months.Single().Expense, days.Value.Total);
            Assert.Equal(days.Value.Total, days.Value.Days.Sum(day => day.Total));
        }

        private Task<Result<DashboardDaysResponseDto>> GetDays(string? month)
            => _financeDashboardService.GetDaysAsync(new DashboardDaysQueryDto { Month = month }, FirstUserId, CancellationToken.None);

        private void SeedPlan(string month, decimal total, params BudgetSlice[] slices)
            => SeedPlan(month, total, FirstUserId, slices);

        private void SeedPlan(string month, decimal total, UserId userId, params BudgetSlice[] slices)
        {
            YearMonth.TryParse(month, out var yearMonth);
            var plan = BudgetPlan.Create(userId.Value, yearMonth, total, slices).Value!;
            plan.AssignId(_nextPlanId++);
            BudgetPlanSingleton.Instance.Add(plan);
        }

        private Task<Result<FinanceDashboardResponseDto>> Get(string? from, string? to, DashboardComparison comparison = DashboardComparison.PreviousPeriod)
            => _financeDashboardService.GetAsync(new FinanceDashboardQueryDto { From = from, To = to, Comparison = comparison }, FirstUserId, CancellationToken.None);

        private MonthlySummary SeedMonth(UserId userId, int year, int month)
        {
            var monthlySummary = MonthlySummary.FromPersistence(_nextMonthId++, userId.Value, month, year, 0, 0, 0);
            MonthlySummarySingleton.Instance.Add(monthlySummary);

            return monthlySummary;
        }

        private void SeedTransaction(MonthlySummary month, MoneyFlowType type, int categoryId, decimal amount, int day = 1)
            => TransactionSingleton.Instance.Add(Transaction.FromPersistence(
                _nextTransactionId++, month.Id!.Value, type, categoryId, amount, "description", new DateOnly(month.Year.Value, month.Month.Value, day)));

        private static void SeedCategory(int id, UserId userId, string name)
        {
            var category = Category.Create(userId.Value, name).Value!;
            category.AssignId(id);
            CategorySingleton.Instance.Add(category);
        }
    }
}
