using LifeManager.Application.Budgets.DTOs;
using LifeManager.Application.Budgets.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Budgets.Errors;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Budgets
{
    [Collection("ApplicationServices")]
    public class BudgetServiceTest : BaseTest
    {
        private static readonly UserId FirstUserId = new(1);
        private static readonly UserId SecondUserId = new(2);

        private const int MarketCategoryId = 1;
        private const int HealthCategoryId = 2;
        private const int TreasuryCategoryId = 3;
        private const int OtherUserCategoryId = 4;

        private readonly BudgetService _budgetService;

        private int _nextMonthId = 1;
        private int _nextTransactionId = 1;

        public BudgetServiceTest()
        {
            _budgetService = ServiceProvider.GetRequiredService<BudgetService>();

            BudgetSingleton.Instance.Clear();
            TransactionSingleton.Instance.Clear();
            MonthlySummarySingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();

            SeedCategory(MarketCategoryId, FirstUserId, "Mercado");
            SeedCategory(HealthCategoryId, FirstUserId, "Farmácia");
            SeedCategory(TreasuryCategoryId, FirstUserId, "Tesouro");
            SeedCategory(OtherUserCategoryId, SecondUserId, "Outros");
        }

        [Fact]
        public async Task SetAsync_ShouldAddTheGoal_FromTheMonthOn()
        {
            var result = await Set(MoneyFlowType.Expense, MarketCategoryId, 1200m, "2026-10");

            Assert.True(result.IsSuccess);
            Assert.Equal(new BudgetResponseDto(1, MoneyFlowType.Expense, MarketCategoryId, "Mercado", 1200m, "2026-10", null), result.Value);
            var stored = Assert.Single(BudgetSingleton.Instance);
            Assert.Equal(FirstUserId, stored.UserId);
        }

        [Fact]
        public async Task SetAsync_ShouldKeepEarlierMonths_WhenTheGoalChangesLater()
        {
            await Set(MoneyFlowType.Expense, MarketCategoryId, 1200m, "2026-10");

            var result = await Set(MoneyFlowType.Expense, MarketCategoryId, 1500m, "2026-12");

            Assert.Equal("2026-12", result.Value!.EffectiveFrom);
            Assert.Equal(1200m, (await GoalOf("2026-11", MarketCategoryId))!.Goal);
            Assert.Equal(1500m, (await GoalOf("2026-12", MarketCategoryId))!.Goal);
            Assert.Equal(1500m, (await GoalOf("2027-06", MarketCategoryId))!.Goal);
            Assert.Null(await GoalOf("2026-09", MarketCategoryId));
        }

        [Fact]
        public async Task SetAsync_ShouldReplaceLaterChanges_WhenSetFromAnEarlierMonth()
        {
            await Set(MoneyFlowType.Expense, MarketCategoryId, 1200m, "2026-10");
            await Set(MoneyFlowType.Expense, MarketCategoryId, 1500m, "2026-12");

            await Set(MoneyFlowType.Expense, MarketCategoryId, 1000m, "2026-11");

            Assert.Equal(1200m, (await GoalOf("2026-10", MarketCategoryId))!.Goal);
            Assert.Equal(1000m, (await GoalOf("2026-11", MarketCategoryId))!.Goal);
            Assert.Equal(1000m, (await GoalOf("2027-01", MarketCategoryId))!.Goal);
            Assert.Equal(2, BudgetSingleton.Instance.Count);
        }

        [Fact]
        public async Task SetAsync_ShouldRewriteTheVersion_WhenSetAgainInTheSameMonth()
        {
            await Set(MoneyFlowType.Investment, null, 2000m, "2026-10");

            var result = await Set(MoneyFlowType.Investment, null, 2500m, "2026-10");

            Assert.Equal(2500m, result.Value!.Amount);
            Assert.Equal(2500m, Assert.Single(BudgetSingleton.Instance).Amount.Value);
        }

        [Fact]
        public async Task SetAsync_ShouldKeepGoalsApart_ByTypeAndCategory()
        {
            await Set(MoneyFlowType.Expense, null, 5000m, "2026-10");
            await Set(MoneyFlowType.Investment, null, 2000m, "2026-10");
            await Set(MoneyFlowType.Expense, MarketCategoryId, 1200m, "2026-10");

            Assert.Equal(3, BudgetSingleton.Instance.Count);
        }

        [Fact]
        public async Task SetAsync_ShouldReturnCategoryNotFound_WhenCategoryBelongsToOtherUser()
        {
            var result = await Set(MoneyFlowType.Expense, OtherUserCategoryId, 100m, "2026-10");

            Assert.Equal(CategoryErrors.NotFound, result.Error);
            Assert.Empty(BudgetSingleton.Instance);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("2026-13")]
        [InlineData("outubro")]
        public async Task SetAsync_ShouldReturnInvalidMonth_WhenFromIsNotYearMonth(string? from)
        {
            var result = await Set(MoneyFlowType.Expense, null, 100m, from);

            Assert.Equal(BudgetErrors.InvalidMonth, result.Error);
        }

        [Fact]
        public async Task SetAsync_ShouldReturnInvalidType_WhenIncome()
        {
            var result = await Set(MoneyFlowType.Income, null, 100m, "2026-10");

            Assert.Equal(BudgetErrors.InvalidType, result.Error);
        }

        [Fact]
        public async Task GetMonthAsync_ShouldCompareEachGoalWithTheMonthsActualAmounts()
        {
            var october = SeedMonth(FirstUserId, 2026, 10);
            SeedTransaction(october, MoneyFlowType.Expense, MarketCategoryId, 1300m);
            SeedTransaction(october, MoneyFlowType.Expense, HealthCategoryId, 100m);
            SeedTransaction(october, MoneyFlowType.Investment, TreasuryCategoryId, 500m);
            SeedTransaction(october, MoneyFlowType.Income, MarketCategoryId, 9000m);
            await Set(MoneyFlowType.Expense, MarketCategoryId, 1200m, "2026-10");
            await Set(MoneyFlowType.Expense, HealthCategoryId, 300m, "2026-09");
            await Set(MoneyFlowType.Expense, null, 5000m, "2026-10");
            await Set(MoneyFlowType.Investment, TreasuryCategoryId, 2000m, "2026-10");

            var result = await _budgetService.GetMonthAsync(new BudgetMonthQueryDto { Month = "2026-10" }, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var month = result.Value;
            Assert.Equal("2026-10", month.Month);

            Assert.Equal(1400m, month.Expenses.Actual);
            var total = month.Expenses.Total!;
            Assert.Equal(5000m, total.Goal);
            Assert.Equal(1400m, total.Actual);
            Assert.Equal(3600m, total.Remaining);
            Assert.Equal(0.28m, total.Ratio);
            Assert.True(total.Achieved);
            Assert.Null(total.CategoryId);

            Assert.Equal(["Farmácia", "Mercado"], month.Expenses.Categories.Select(category => category.CategoryName!).ToArray());
            var market = month.Expenses.Categories[1];
            Assert.Equal(-100m, market.Remaining);
            Assert.False(market.Achieved);
            Assert.Equal("2026-09", month.Expenses.Categories[0].EffectiveFrom);

            Assert.Null(month.Investments.Total);
            Assert.Equal(500m, month.Investments.Actual);
            var treasury = Assert.Single(month.Investments.Categories);
            Assert.Equal(0.25m, treasury.Ratio);
            Assert.False(treasury.Achieved);
        }

        [Fact]
        public async Task GetMonthAsync_ShouldReturnGoalsWithZeroActual_WhenTheMonthIsNotOpen()
        {
            await Set(MoneyFlowType.Investment, null, 2000m, "2026-10");

            var result = await _budgetService.GetMonthAsync(new BudgetMonthQueryDto { Month = "2027-03" }, FirstUserId, CancellationToken.None);

            var total = result.Value!.Investments.Total!;
            Assert.Equal(0m, total.Actual);
            Assert.False(total.Achieved);
            Assert.Equal(2000m, total.Remaining);
        }

        [Fact]
        public async Task GetMonthAsync_ShouldNotShowOtherUsersGoals()
        {
            await _budgetService.SetAsync(new BudgetDto(MoneyFlowType.Expense, null, "2026-10", 100m), SecondUserId, CancellationToken.None);

            var result = await _budgetService.GetMonthAsync(new BudgetMonthQueryDto { Month = "2026-10" }, FirstUserId, CancellationToken.None);

            Assert.Null(result.Value!.Expenses.Total);
        }

        [Fact]
        public async Task GetMonthAsync_ShouldReturnInvalidMonth_WhenMonthIsMissing()
        {
            var result = await _budgetService.GetMonthAsync(new BudgetMonthQueryDto(), FirstUserId, CancellationToken.None);

            Assert.Equal(BudgetErrors.InvalidMonth, result.Error);
        }

        [Fact]
        public async Task RemoveAsync_ShouldRemoveTheGoalFromTheMonthOn_AndKeepEarlierMonths()
        {
            var created = await Set(MoneyFlowType.Expense, MarketCategoryId, 1200m, "2026-10");

            var result = await _budgetService.RemoveAsync(created.Value!.Id, new BudgetMonthQueryDto { Month = "2026-12" }, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(await GoalOf("2026-11", MarketCategoryId));
            Assert.Null(await GoalOf("2026-12", MarketCategoryId));
            Assert.Null(await GoalOf("2027-05", MarketCategoryId));
        }

        [Fact]
        public async Task RemoveAsync_ShouldDeleteTheGoal_WhenRemovedFromItsFirstMonth()
        {
            var created = await Set(MoneyFlowType.Expense, MarketCategoryId, 1200m, "2026-10");

            await _budgetService.RemoveAsync(created.Value!.Id, new BudgetMonthQueryDto { Month = "2026-10" }, FirstUserId, CancellationToken.None);

            Assert.Empty(BudgetSingleton.Instance);
        }

        [Fact]
        public async Task RemoveAsync_ShouldReturnNotFound_WhenGoalBelongsToOtherUser()
        {
            var created = await Set(MoneyFlowType.Expense, MarketCategoryId, 1200m, "2026-10");

            var result = await _budgetService.RemoveAsync(created.Value!.Id, new BudgetMonthQueryDto { Month = "2026-10" }, SecondUserId, CancellationToken.None);

            Assert.Equal(BudgetErrors.NotFound, result.Error);
            Assert.Single(BudgetSingleton.Instance);
        }

        [Fact]
        public async Task RemoveAsync_ShouldReturnNotFound_WhenThereIsNoGoalFromThatMonth()
        {
            var created = await Set(MoneyFlowType.Expense, MarketCategoryId, 1200m, "2026-10");
            await _budgetService.RemoveAsync(created.Value!.Id, new BudgetMonthQueryDto { Month = "2026-12" }, FirstUserId, CancellationToken.None);

            var result = await _budgetService.RemoveAsync(created.Value.Id, new BudgetMonthQueryDto { Month = "2027-01" }, FirstUserId, CancellationToken.None);

            Assert.Equal(BudgetErrors.NotFound, result.Error);
        }

        private Task<Result<BudgetResponseDto>> Set(MoneyFlowType type, int? categoryId, decimal amount, string? from)
            => _budgetService.SetAsync(new BudgetDto(type, categoryId, from, amount), FirstUserId, CancellationToken.None);

        private async Task<BudgetProgressDto?> GoalOf(string month, int categoryId)
        {
            var result = await _budgetService.GetMonthAsync(new BudgetMonthQueryDto { Month = month }, FirstUserId, CancellationToken.None);

            return result.Value!.Expenses.Categories.SingleOrDefault(category => category.CategoryId == categoryId);
        }

        private MonthlySummary SeedMonth(UserId userId, int year, int month)
        {
            var monthlySummary = MonthlySummary.FromPersistence(_nextMonthId++, userId.Value, month, year, 0, 0, 0);
            MonthlySummarySingleton.Instance.Add(monthlySummary);

            return monthlySummary;
        }

        private void SeedTransaction(MonthlySummary month, MoneyFlowType type, int categoryId, decimal amount)
            => TransactionSingleton.Instance.Add(Transaction.FromPersistence(
                _nextTransactionId++, month.Id!.Value, type, categoryId, amount, "description", new DateOnly(month.Year.Value, month.Month.Value, 1)));

        private static void SeedCategory(int id, UserId userId, string name)
        {
            var category = Category.Create(userId.Value, name).Value!;
            category.AssignId(id);
            CategorySingleton.Instance.Add(category);
        }
    }
}
