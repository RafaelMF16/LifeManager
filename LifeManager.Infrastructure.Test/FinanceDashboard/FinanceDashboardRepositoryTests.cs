using LifeManager.Domain.FinanceDashboard;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Infrastructure.FinanceDashboard;
using LifeManager.Infrastructure.Test.Seed;
using LifeManager.Infrastructure.Transactions;

namespace LifeManager.Infrastructure.Test.FinanceDashboard
{
    public class FinanceDashboardRepositoryTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        private static readonly DateOnly From = new(2026, 10, 1);
        private static readonly DateOnly To = new(2026, 11, 30);

        [Fact]
        public async Task GetCategoryMonthTotalsAsync_ShouldSumEachCategoryTypeAndMonth_WhenThePeriodHasTransactions()
        {
            await using var context = Fixture.CreateContext();
            var (userId, market, salary) = await SeedAsync(context);

            var totals = await new FinanceDashboardRepository(context).GetCategoryMonthTotalsAsync(userId, From, To, default);

            var expected = new[]
            {
                new DashboardCategoryMonthTotal(market, "Mercado", MoneyFlowType.Expense, 2026, 10, 150m),
                new DashboardCategoryMonthTotal(market, "Mercado", MoneyFlowType.Expense, 2026, 11, 75.25m),
                new DashboardCategoryMonthTotal(market, "Mercado", MoneyFlowType.Investment, 2026, 11, 300m),
                new DashboardCategoryMonthTotal(salary, "Salário", MoneyFlowType.Income, 2026, 11, 5000m),
            };
            Assert.Equal(Sorted(expected), Sorted(totals));
        }

        [Fact]
        public async Task GetExpenseDayTotalsAsync_ShouldSumOnlyExpensesPerDay_WhenTheMonthHasTransactions()
        {
            await using var context = Fixture.CreateContext();
            var (userId, _, _) = await SeedAsync(context);

            var days = await new FinanceDashboardRepository(context)
                .GetExpenseDayTotalsAsync(userId, new DateOnly(2026, 11, 1), To, default);

            Assert.Equal(
                [new DashboardDayTotal(new DateOnly(2026, 11, 3), 75.25m, 2)],
                days.OrderBy(day => day.Date).ToList());
        }

        /// <summary>
        /// Oct/Nov transactions of the user in two categories, plus what must be left out: a September transaction
        /// (before the period) and another user's November expense.
        /// </summary>
        private static async Task<(Domain.Users.ValueObjects.UserId UserId, int Market, int Salary)> SeedAsync(Postgres.LifeManagerDbContext context)
        {
            var user = await context.AddUserAsync();
            var market = await context.AddCategoryAsync(user, "Mercado");
            var salary = await context.AddCategoryAsync(user, "Salário");
            var september = await context.AddMonthAsync(user, 2026, 9);
            var october = await context.AddMonthAsync(user, 2026, 10);
            var november = await context.AddMonthAsync(user, 2026, 11);

            var otherUser = await context.AddUserAsync("Bia");
            var otherCategory = await context.AddCategoryAsync(otherUser, "Mercado");
            var otherNovember = await context.AddMonthAsync(otherUser, 2026, 11);

            var repository = new TransactionRepository(context);
            foreach (var transaction in new[]
            {
                TestData.NewTransaction(september, market, MoneyFlowType.Expense, 999m, day: 30),
                TestData.NewTransaction(october, market, MoneyFlowType.Expense, 100m, day: 2),
                TestData.NewTransaction(october, market, MoneyFlowType.Expense, 50m, day: 31),
                TestData.NewTransaction(november, market, MoneyFlowType.Expense, 70m, day: 3),
                TestData.NewTransaction(november, market, MoneyFlowType.Expense, 5.25m, day: 3),
                TestData.NewTransaction(november, market, MoneyFlowType.Investment, 300m, day: 3),
                TestData.NewTransaction(november, salary, MoneyFlowType.Income, 5000m, day: 5),
                TestData.NewTransaction(otherNovember, otherCategory, MoneyFlowType.Expense, 888m, day: 3),
            })
            {
                await repository.AddAsync(transaction, default);
            }

            return (user.Id!, market.Id!.Value, salary.Id!.Value);
        }

        private static List<DashboardCategoryMonthTotal> Sorted(IEnumerable<DashboardCategoryMonthTotal> totals)
            => [.. totals.OrderBy(total => total.CategoryId).ThenBy(total => total.Type).ThenBy(total => total.Year).ThenBy(total => total.Month)];
    }
}
