using LifeManager.Application.BudgetPlans.DTOs;
using LifeManager.Application.BudgetPlans.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.BudgetPlans;
using LifeManager.Domain.BudgetPlans.Errors;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.BudgetPlans
{
    [Collection("ApplicationServices")]
    public class BudgetPlanServiceTest : BaseTest
    {
        private static readonly UserId FirstUserId = new(1);
        private static readonly UserId SecondUserId = new(2);

        private const int MarketCategoryId = 1;
        private const int HousingCategoryId = 2;
        private const int LeisureCategoryId = 3;
        private const int TreasuryCategoryId = 4;
        private const int OtherUserCategoryId = 5;

        private readonly BudgetPlanService _budgetPlanService;

        private int _nextMonthId = 1;
        private int _nextTransactionId = 1;

        public BudgetPlanServiceTest()
        {
            _budgetPlanService = ServiceProvider.GetRequiredService<BudgetPlanService>();

            BudgetPlanSingleton.Instance.Clear();
            TransactionSingleton.Instance.Clear();
            MonthlySummarySingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();

            SeedCategory(MarketCategoryId, FirstUserId, "Mercado");
            SeedCategory(HousingCategoryId, FirstUserId, "Moradia");
            SeedCategory(LeisureCategoryId, FirstUserId, "Lazer");
            SeedCategory(TreasuryCategoryId, FirstUserId, "Tesouro");
            SeedCategory(OtherUserCategoryId, SecondUserId, "Outros");
        }

        [Fact]
        public async Task GetAsync_ShouldReturnEachSliceWithItsAmountAndActual()
        {
            SeedPlan(FirstUserId, "2026-10", 6000m, Expense(HousingCategoryId, 30m), Expense(MarketCategoryId, 25m), Investment(20m));
            var october = SeedMonth(FirstUserId, 2026, 10);
            SeedTransaction(october, MoneyFlowType.Expense, MarketCategoryId, 1250m);
            SeedTransaction(october, MoneyFlowType.Expense, HousingCategoryId, 1900m);
            SeedTransaction(october, MoneyFlowType.Investment, TreasuryCategoryId, 1200m);

            var result = await _budgetPlanService.GetAsync("2026-10", FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var plan = result.Value;
            Assert.Equal("2026-10", plan.Month);
            Assert.True(plan.Exists);
            Assert.Equal(6000m, plan.Total);
            Assert.Equal(
                [
                    new BudgetSliceProgressDto(MoneyFlowType.Expense, MarketCategoryId, "Mercado", 25m, 1500m, 1250m, 0.8333m, true),
                    new BudgetSliceProgressDto(MoneyFlowType.Expense, HousingCategoryId, "Moradia", 30m, 1800m, 1900m, 1.0556m, false),
                    new BudgetSliceProgressDto(MoneyFlowType.Investment, null, null, 20m, 1200m, 1200m, 1m, true),
                ],
                plan.Slices);
            Assert.Equal(25m, plan.FreePercent);
            Assert.Equal(1500m, plan.FreeAmount);
        }

        [Fact]
        public async Task GetAsync_ShouldCountOnlyExpensesOutsideTheSlices_AsFreeActual()
        {
            SeedPlan(FirstUserId, "2026-10", 6000m, Expense(MarketCategoryId, 25m), Investment(20m));
            var october = SeedMonth(FirstUserId, 2026, 10);
            SeedTransaction(october, MoneyFlowType.Expense, MarketCategoryId, 1000m);
            SeedTransaction(october, MoneyFlowType.Expense, LeisureCategoryId, 200m);
            SeedTransaction(october, MoneyFlowType.Expense, HousingCategoryId, 100m);
            SeedTransaction(october, MoneyFlowType.Income, LeisureCategoryId, 9000m);
            SeedTransaction(october, MoneyFlowType.Investment, TreasuryCategoryId, 500m);

            var plan = (await _budgetPlanService.GetAsync("2026-10", FirstUserId, CancellationToken.None)).Value!;

            Assert.Equal(300m, plan.FreeActual);
        }

        [Fact]
        public async Task GetAsync_ShouldCompareTheInvestmentSliceWithEverythingInvested()
        {
            SeedPlan(FirstUserId, "2026-10", 6000m, Expense(TreasuryCategoryId, 10m), Investment(20m));
            var october = SeedMonth(FirstUserId, 2026, 10);
            SeedTransaction(october, MoneyFlowType.Investment, TreasuryCategoryId, 700m);
            SeedTransaction(october, MoneyFlowType.Investment, MarketCategoryId, 300m);

            var plan = (await _budgetPlanService.GetAsync("2026-10", FirstUserId, CancellationToken.None)).Value!;

            Assert.Equal(1000m, plan.Slices.Single(slice => slice.Type == MoneyFlowType.Investment).Actual);
            Assert.Equal(0m, plan.Slices.Single(slice => slice.Type == MoneyFlowType.Expense).Actual);
        }

        [Fact]
        public async Task GetAsync_ShouldOnlyCountTheMonthsTransactions()
        {
            SeedPlan(FirstUserId, "2026-10", 6000m, Expense(MarketCategoryId, 25m));
            SeedTransaction(SeedMonth(FirstUserId, 2026, 9), MoneyFlowType.Expense, MarketCategoryId, 999m);
            SeedTransaction(SeedMonth(FirstUserId, 2026, 10), MoneyFlowType.Expense, MarketCategoryId, 100m);

            var plan = (await _budgetPlanService.GetAsync("2026-10", FirstUserId, CancellationToken.None)).Value!;

            Assert.Equal(100m, Assert.Single(plan.Slices).Actual);
        }

        [Fact]
        public async Task GetAsync_ShouldReturnAnEmptyMonth_WhenThereIsNoPlan()
        {
            SeedTransaction(SeedMonth(FirstUserId, 2026, 11), MoneyFlowType.Expense, MarketCategoryId, 400m);

            var plan = (await _budgetPlanService.GetAsync("2026-11", FirstUserId, CancellationToken.None)).Value!;

            Assert.False(plan.Exists);
            Assert.Null(plan.Total);
            Assert.Empty(plan.Slices);
            Assert.Equal(100m, plan.FreePercent);
            Assert.Null(plan.FreeAmount);
            Assert.Equal(400m, plan.FreeActual);
            Assert.Null(plan.CopySource);
        }

        [Fact]
        public async Task GetAsync_ShouldOfferTheLatestEarlierPlanToCopy_SkippingEmptyMonths()
        {
            SeedPlan(FirstUserId, "2026-08", 5000m);
            SeedPlan(FirstUserId, "2026-10", 6000m);
            SeedPlan(FirstUserId, "2027-01", 6000m);

            var plan = (await _budgetPlanService.GetAsync("2026-12", FirstUserId, CancellationToken.None)).Value!;

            Assert.Equal("2026-10", plan.CopySource);
        }

        [Fact]
        public async Task GetAsync_ShouldOfferTheLatestEarlierPlan_EvenWhenTheMonthHasOne()
        {
            SeedPlan(FirstUserId, "2026-10", 6000m);
            SeedPlan(FirstUserId, "2026-11", 6000m);

            var plan = (await _budgetPlanService.GetAsync("2026-11", FirstUserId, CancellationToken.None)).Value!;

            Assert.Equal("2026-10", plan.CopySource);
        }

        [Fact]
        public async Task GetAsync_ShouldNotSeeOtherUsersPlans()
        {
            SeedPlan(SecondUserId, "2026-10", 6000m, Expense(OtherUserCategoryId, 25m));
            SeedPlan(SecondUserId, "2026-09", 6000m);

            var plan = (await _budgetPlanService.GetAsync("2026-10", FirstUserId, CancellationToken.None)).Value!;

            Assert.False(plan.Exists);
            Assert.Null(plan.CopySource);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("2026-13")]
        [InlineData("outubro")]
        public async Task GetAsync_ShouldReturnInvalidMonth_WhenNotYearMonth(string? month)
        {
            var result = await _budgetPlanService.GetAsync(month, FirstUserId, CancellationToken.None);

            Assert.Equal(BudgetPlanErrors.InvalidMonth, result.Error);
        }

        [Fact]
        public async Task SaveAsync_ShouldCreateTheMonthsPlan_AndReturnItLikeGet()
        {
            var result = await Save("2026-10", 6000m, new BudgetSliceDto(MoneyFlowType.Expense, MarketCategoryId, 25m), new BudgetSliceDto(MoneyFlowType.Investment, null, 20m));

            Assert.True(result.IsSuccess);
            Assert.True(result.Value.Exists);
            Assert.Equal(6000m, result.Value.Total);
            Assert.Equal([1500m, 1200m], result.Value.Slices.Select(slice => slice.Amount));
            Assert.Equal(55m, result.Value.FreePercent);
            var stored = Assert.Single(BudgetPlanSingleton.Instance);
            Assert.Equal(FirstUserId, stored.UserId);
            Assert.Equal(new DateOnly(2026, 10, 1), stored.MonthStart);
        }

        [Fact]
        public async Task SaveAsync_ShouldReplaceTheWholePlan_WhenTheMonthAlreadyHasOne()
        {
            SeedPlan(FirstUserId, "2026-10", 6000m, Expense(MarketCategoryId, 25m), Investment(20m));

            var result = await Save("2026-10", 8000m, new BudgetSliceDto(MoneyFlowType.Expense, HousingCategoryId, 30m));

            Assert.True(result.IsSuccess);
            var stored = Assert.Single(BudgetPlanSingleton.Instance);
            Assert.Equal(8000m, stored.Total.Value);
            Assert.Equal(HousingCategoryId, Assert.Single(stored.Slices).CategoryId!.Value);
        }

        [Fact]
        public async Task SaveAsync_ShouldNotTouchOtherMonths()
        {
            SeedPlan(FirstUserId, "2026-09", 5000m, Expense(MarketCategoryId, 10m));
            SeedPlan(FirstUserId, "2026-11", 7000m, Expense(MarketCategoryId, 40m));

            await Save("2026-10", 6000m, new BudgetSliceDto(MoneyFlowType.Expense, MarketCategoryId, 25m));

            Assert.Equal(
                [("2026-09", 5000m, 10m), ("2026-10", 6000m, 25m), ("2026-11", 7000m, 40m)],
                BudgetPlanSingleton.Instance
                    .OrderBy(plan => plan.MonthStart)
                    .Select(plan => (plan.Month.ToString(), plan.Total.Value, plan.Slices.Single().Percent.Value)));
        }

        [Fact]
        public async Task SaveAsync_ShouldAcceptAPlanWithoutSlices()
        {
            var result = await Save("2026-10", 6000m);

            Assert.Equal(100m, result.Value!.FreePercent);
            Assert.Equal(6000m, result.Value.FreeAmount);
        }

        [Fact]
        public async Task SaveAsync_ShouldReturnOverAllocated_AndKeepThePlan_WhenSlicesPassAHundred()
        {
            SeedPlan(FirstUserId, "2026-10", 6000m, Expense(MarketCategoryId, 25m));

            var result = await Save("2026-10", 6000m, new BudgetSliceDto(MoneyFlowType.Expense, MarketCategoryId, 80m), new BudgetSliceDto(MoneyFlowType.Investment, null, 20.01m));

            Assert.Equal(BudgetPlanErrors.OverAllocated, result.Error);
            Assert.Equal(25m, Assert.Single(Assert.Single(BudgetPlanSingleton.Instance).Slices).Percent.Value);
        }

        [Fact]
        public async Task SaveAsync_ShouldReturnCategoryNotFound_WhenACategoryBelongsToOtherUser()
        {
            var result = await Save("2026-10", 6000m, new BudgetSliceDto(MoneyFlowType.Expense, MarketCategoryId, 25m), new BudgetSliceDto(MoneyFlowType.Expense, OtherUserCategoryId, 10m));

            Assert.Equal(CategoryErrors.NotFound, result.Error);
            Assert.Empty(BudgetPlanSingleton.Instance);
        }

        [Theory]
        [InlineData(0, "BudgetPlan.TotalNotPositive")]
        [InlineData(10.123, "BudgetPlan.TotalTooManyDecimals")]
        public async Task SaveAsync_ShouldReturnTheTotalError_WhenTheTotalIsInvalid(decimal total, string expectedCode)
        {
            var result = await Save("2026-10", total);

            Assert.Equal(expectedCode, result.Error!.Code);
        }

        [Fact]
        public async Task SaveAsync_ShouldReturnTheSliceError_WhenASliceIsInvalid()
        {
            var result = await Save("2026-10", 6000m, new BudgetSliceDto(MoneyFlowType.Expense, null, 25m));

            Assert.Equal(BudgetPlanErrors.ExpenseSliceWithoutCategory, result.Error);
        }

        [Fact]
        public async Task SaveAsync_ShouldTreatMissingSlicesAsNone()
        {
            var result = await _budgetPlanService.SaveAsync("2026-10", new BudgetPlanDto(6000m, null), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value.Slices);
        }

        [Fact]
        public async Task SaveAsync_ShouldReturnInvalidMonth_WhenNotYearMonth()
        {
            var result = await Save("2026-13", 6000m);

            Assert.Equal(BudgetPlanErrors.InvalidMonth, result.Error);
        }

        [Fact]
        public async Task DeleteAsync_ShouldDeleteOnlyThatMonthsPlan()
        {
            SeedPlan(FirstUserId, "2026-10", 6000m);
            SeedPlan(FirstUserId, "2026-11", 6000m);
            SeedPlan(SecondUserId, "2026-10", 6000m);

            var result = await _budgetPlanService.DeleteAsync("2026-10", FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                [(FirstUserId, "2026-11"), (SecondUserId, "2026-10")],
                BudgetPlanSingleton.Instance.Select(plan => (plan.UserId, plan.Month.ToString())));
        }

        [Fact]
        public async Task DeleteAsync_ShouldReturnNotFound_WhenTheMonthHasNoPlan()
        {
            SeedPlan(SecondUserId, "2026-10", 6000m);

            var result = await _budgetPlanService.DeleteAsync("2026-10", FirstUserId, CancellationToken.None);

            Assert.Equal(BudgetPlanErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task DeleteAsync_ShouldReturnInvalidMonth_WhenNotYearMonth()
        {
            var result = await _budgetPlanService.DeleteAsync("outubro", FirstUserId, CancellationToken.None);

            Assert.Equal(BudgetPlanErrors.InvalidMonth, result.Error);
        }

        private Task<Result<BudgetPlanResponseDto>> Save(string month, decimal total, params BudgetSliceDto[] slices)
            => _budgetPlanService.SaveAsync(month, new BudgetPlanDto(total, slices), FirstUserId, CancellationToken.None);

        private static BudgetSlice Expense(int categoryId, decimal percent)
            => BudgetSlice.Create(MoneyFlowType.Expense, categoryId, percent).Value!;

        private static BudgetSlice Investment(decimal percent)
            => BudgetSlice.Create(MoneyFlowType.Investment, null, percent).Value!;

        private static void SeedPlan(UserId userId, string month, decimal total, params BudgetSlice[] slices)
        {
            YearMonth.TryParse(month, out var yearMonth);
            var plan = BudgetPlan.Create(userId.Value, yearMonth, total, slices).Value!;
            plan.AssignId(BudgetPlanSingleton.Instance.Count + 1);
            BudgetPlanSingleton.Instance.Add(plan);
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
