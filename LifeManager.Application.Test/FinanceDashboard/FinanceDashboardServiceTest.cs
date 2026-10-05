using LifeManager.Application.FinanceDashboard.DTOs;
using LifeManager.Application.FinanceDashboard.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Categories;
using LifeManager.Domain.FinanceDashboard.Enums;
using LifeManager.Domain.FinanceDashboard.Errors;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;
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

        public FinanceDashboardServiceTest()
        {
            _financeDashboardService = ServiceProvider.GetRequiredService<FinanceDashboardService>();

            TransactionSingleton.Instance.Clear();
            MonthlySummarySingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();

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
                [new DashboardMonthDto(2026, 4, 0, 0, 0, 0), new DashboardMonthDto(2026, 5, 0, 0, 0, 0), new DashboardMonthDto(2026, 6, 0, 0, 0, 0)],
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
            Assert.Equal([new DashboardMonthDto(2026, 4, 5000m, 1500m, 1000m, 2500m)], result.Value.Months);
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
