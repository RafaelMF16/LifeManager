using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Infrastructure.Test.Seed;
using LifeManager.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Test.Transactions
{
    public class TransactionRepositoryTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        [Fact]
        public async Task AddAsync_ShouldRecalculateEveryTotal_WhenTransactionsOfEachTypeAreAdded()
        {
            var (month, category) = await SeedMonthAsync();

            await using (var context = Fixture.CreateContext())
            {
                var repository = new TransactionRepository(context);
                await repository.AddAsync(TestData.NewTransaction(month, category, MoneyFlowType.Income, 5000m), default);
                await repository.AddAsync(TestData.NewTransaction(month, category, MoneyFlowType.Expense, 1200.50m), default);
                await repository.AddAsync(TestData.NewTransaction(month, category, MoneyFlowType.Investment, 800m), default);
            }

            var stored = await ReadMonthAsync(month);
            Assert.Equal(5000m, stored.TotalIncome.Value);
            Assert.Equal(1200.50m, stored.TotalExpense.Value);
            Assert.Equal(800m, stored.TotalInvestment.Value);
            Assert.Equal(2999.50m, stored.BalanceAmount);
        }

        [Fact]
        public async Task UpdateAsync_ShouldRecalculateTotals_WhenTypeAndAmountChange()
        {
            var (month, category) = await SeedMonthAsync();
            var transaction = TestData.NewTransaction(month, category, MoneyFlowType.Expense, 100m);

            await using (var context = Fixture.CreateContext())
                await new TransactionRepository(context).AddAsync(transaction, default);

            transaction.Update(MoneyFlowType.Income, category.Id!.Value, 250m, "Reembolso", transaction.TransactionDate, month).Unwrap();
            await using (var context = Fixture.CreateContext())
                await new TransactionRepository(context).UpdateAsync(transaction, default);

            var stored = await ReadMonthAsync(month);
            Assert.Equal(250m, stored.TotalIncome.Value);
            Assert.Equal(0m, stored.TotalExpense.Value);
            Assert.Equal(250m, stored.BalanceAmount);
        }

        [Fact]
        public async Task DeleteAsync_ShouldRecalculateTotals_WhenTransactionIsDeleted()
        {
            var (month, category) = await SeedMonthAsync();
            var kept = TestData.NewTransaction(month, category, MoneyFlowType.Expense, 40m);
            var deleted = TestData.NewTransaction(month, category, MoneyFlowType.Expense, 60m);

            await using (var context = Fixture.CreateContext())
            {
                var repository = new TransactionRepository(context);
                await repository.AddAsync(kept, default);
                await repository.AddAsync(deleted, default);
            }

            bool wasDeleted;
            await using (var context = Fixture.CreateContext())
                wasDeleted = await new TransactionRepository(context).DeleteAsync(deleted.Id!, month.Id!, default);

            var stored = await ReadMonthAsync(month);
            Assert.True(wasDeleted);
            Assert.Equal(40m, stored.TotalExpense.Value);
            Assert.Equal(-40m, stored.BalanceAmount);
        }

        [Fact]
        public async Task AddAsync_ShouldKeepExactTotals_WhenTenWritesToTheSameMonthRunConcurrently()
        {
            var (month, category) = await SeedMonthAsync();

            // One DbContext per task: the month's FOR UPDATE lock is what keeps the recalculations from interleaving.
            await Task.WhenAll(Enumerable.Range(1, 10).Select(async amount =>
            {
                await using var context = Fixture.CreateContext();
                await new TransactionRepository(context)
                    .AddAsync(TestData.NewTransaction(month, category, MoneyFlowType.Expense, amount), default);
            }));

            await using var readContext = Fixture.CreateContext();
            var count = await readContext.Transactions.CountAsync(transaction => transaction.MonthlySummaryId == month.Id);
            var stored = await ReadMonthAsync(month);
            Assert.Equal(10, count);
            Assert.Equal(55m, stored.TotalExpense.Value);
            Assert.Equal(-55m, stored.BalanceAmount);
        }

        private async Task<(MonthlySummary Month, Domain.Categories.Category Category)> SeedMonthAsync()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var category = await context.AddCategoryAsync(user);
            var month = await context.AddMonthAsync(user, 2026, 11);

            return (month, category);
        }

        private async Task<MonthlySummary> ReadMonthAsync(MonthlySummary month)
        {
            await using var context = Fixture.CreateContext();

            return await context.MonthlySummaries.AsNoTracking().SingleAsync(stored => stored.Id == month.Id);
        }
    }
}
