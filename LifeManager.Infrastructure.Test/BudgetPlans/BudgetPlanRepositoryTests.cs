using LifeManager.Domain.BudgetPlans;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Infrastructure.BudgetPlans;
using LifeManager.Infrastructure.Test.Seed;

namespace LifeManager.Infrastructure.Test.BudgetPlans
{
    public class BudgetPlanRepositoryTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        [Fact]
        public async Task GetWithSliceSpendingAsync_ShouldSumEachExpenseSlicesCategoryInTheMonthOnly()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var market = await context.AddCategoryAsync(user, "Mercado");
            var housing = await context.AddCategoryAsync(user, "Moradia");
            var leisure = await context.AddCategoryAsync(user, "Lazer");
            var november = await context.AddMonthAsync(user, 2026, 11);
            var october = await context.AddMonthAsync(user, 2026, 10);
            context.AddRange(
                TestData.NewTransaction(november, market, MoneyFlowType.Expense, 100m),
                TestData.NewTransaction(november, market, MoneyFlowType.Expense, 50.25m, day: 30),
                // Not this month, not an expense, or a category without a slice: none of them count.
                TestData.NewTransaction(october, market, MoneyFlowType.Expense, 999m),
                TestData.NewTransaction(november, market, MoneyFlowType.Investment, 300m),
                TestData.NewTransaction(november, leisure, MoneyFlowType.Expense, 80m));
            var slices = new[]
            {
                BudgetSlice.Create(MoneyFlowType.Expense, market.Id!.Value, 25m).Unwrap(),
                BudgetSlice.Create(MoneyFlowType.Expense, housing.Id!.Value, 30m).Unwrap(),
                BudgetSlice.Create(MoneyFlowType.Investment, null, 20m).Unwrap(),
            };
            context.Add(BudgetPlan.Create(user.Id!.Value, TestData.Month(2026, 11), 6000m, slices, TestData.Now).Unwrap());
            await context.SaveChangesAsync();

            await using var readContext = Fixture.CreateContext();
            var spending = await new BudgetPlanRepository(readContext).GetWithSliceSpendingAsync(user.Id!, TestData.Month(2026, 11), default);

            Assert.NotNull(spending);
            Assert.Equal(6000m, spending.Plan.Total.Value);
            Assert.Equal(
                [new BudgetSliceSpending(market.Id.Value, "Mercado", 150.25m), new BudgetSliceSpending(housing.Id.Value, "Moradia", 0m)],
                spending.ExpenseSlices.OrderBy(slice => slice.CategoryName));
        }

        [Fact]
        public async Task GetWithSliceSpendingAsync_ShouldReturnNull_WithoutAPlanForTheMonth()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();

            Assert.Null(await new BudgetPlanRepository(context).GetWithSliceSpendingAsync(user.Id!, TestData.Month(2026, 11), default));
        }
    }
}
