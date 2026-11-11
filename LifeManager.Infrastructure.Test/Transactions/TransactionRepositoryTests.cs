using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.Rewards;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Transactions.Enums;
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

        [Fact]
        public async Task StreamForExportAsync_ShouldReadOnlyTheUsersPeriodAndType_ByDateThenId()
        {
            Domain.Users.User user;
            Domain.Categories.Category market, salary;
            await using (var context = Fixture.CreateContext())
            {
                user = await context.AddUserAsync("Ana");
                market = await context.AddCategoryAsync(user, "Mercado");
                salary = await context.AddCategoryAsync(user, "Salário");
                var october = await context.AddMonthAsync(user, 2026, 10);
                var november = await context.AddMonthAsync(user, 2026, 11);
                var december = await context.AddMonthAsync(user, 2026, 12);

                var other = await context.AddUserAsync("Bia");
                var otherCategory = await context.AddCategoryAsync(other, "Outros");
                var otherNovember = await context.AddMonthAsync(other, 2026, 11);

                var repository = new TransactionRepository(context);
                await repository.AddAsync(TestData.NewTransaction(october, market, MoneyFlowType.Expense, 1m, 31, "Fora antes"), default);
                await repository.AddAsync(TestData.NewTransaction(november, market, MoneyFlowType.Expense, 30m, 10, "Feira"), default);
                await repository.AddAsync(TestData.NewTransaction(november, salary, MoneyFlowType.Income, 5000m, 5, "Salário"), default);
                await repository.AddAsync(TestData.NewTransaction(november, market, MoneyFlowType.Expense, 20m, 10, "Padaria"), default);
                await repository.AddAsync(TestData.NewTransaction(november, market, MoneyFlowType.Investment, 300m, 20, "Tesouro"), default);
                await repository.AddAsync(TestData.NewTransaction(december, market, MoneyFlowType.Expense, 1m, 1, "Fora depois"), default);
                await repository.AddAsync(TestData.NewTransaction(otherNovember, otherCategory, MoneyFlowType.Expense, 99m, 10, "Alheio"), default);
            }

            var from = new DateOnly(2026, 11, 1);
            var to = new DateOnly(2026, 11, 30);
            await using var readContext = Fixture.CreateContext();
            var reader = new TransactionRepository(readContext);

            var all = await reader.StreamForExportAsync(user.Id!, from, to, TransactionTypeFilter.All, default).ToListAsync();
            var expenses = await reader.StreamForExportAsync(user.Id!, from, to, TransactionTypeFilter.Expense, default).ToListAsync();

            Assert.Equal(
                [
                    new TransactionExportRow(new DateOnly(2026, 11, 5), "Salário", "Salário", MoneyFlowType.Income, 5000m, TransactionOrigin.Manual),
                    new TransactionExportRow(new DateOnly(2026, 11, 10), "Feira", "Mercado", MoneyFlowType.Expense, -30m, TransactionOrigin.Manual),
                    new TransactionExportRow(new DateOnly(2026, 11, 10), "Padaria", "Mercado", MoneyFlowType.Expense, -20m, TransactionOrigin.Manual),
                    new TransactionExportRow(new DateOnly(2026, 11, 20), "Tesouro", "Mercado", MoneyFlowType.Investment, -300m, TransactionOrigin.Manual),
                ],
                all);
            Assert.Equal(["Feira", "Padaria"], expenses.Select(row => row.Description));
            Assert.Equal(4, await reader.CountForExportAsync(user.Id!, from, to, TransactionTypeFilter.All, default));
            Assert.Equal(2, await reader.CountForExportAsync(user.Id!, from, to, TransactionTypeFilter.Expense, default));
            Assert.Equal(1, await reader.CountForExportAsync(user.Id!, from, to, TransactionTypeFilter.Income, default));
        }

        [Fact]
        public async Task StreamForExportAsync_ShouldTellEachOrigin_EvenAfterTheImportLeftTheHistory()
        {
            Domain.Users.User user;
            await using (var context = Fixture.CreateContext())
            {
                user = await context.AddUserWithEverythingAsync("Ana");
                var category = await context.Categories.SingleAsync(stored => stored.UserId == user.Id);
                var month = await context.MonthlySummaries.SingleAsync(stored => stored.UserId == user.Id);

                var reward = await context.Rewards.SingleAsync(stored => stored.UserId == user.Id);
                var expense = Transaction.CreateFromRedemption(reward, new DateOnly(2026, 11, 9), month).Unwrap();
                await new TransactionRepository(context).AddAsync(expense, default);
                var redemption = RewardRedemption.Create(reward, new DateOnly(2026, 11, 9), TestData.Now).Unwrap();
                redemption.AttachExpense(expense.Id!);
                context.Add(redemption);
                await context.SaveChangesAsync();
            }

            var expected = new Dictionary<string, TransactionOrigin>
            {
                ["Compra"] = TransactionOrigin.Manual,
                ["Academia"] = TransactionOrigin.Recurring,
                ["Padaria"] = TransactionOrigin.Import,
                ["Cinema"] = TransactionOrigin.Reward,
            };
            Assert.Equal(expected, await OriginsAsync(user));

            // Removing the import from the history nulls the transaction's StatementImportId (FK SET NULL); the bank line stays.
            await using (var context = Fixture.CreateContext())
                await context.StatementImports.Where(import => import.UserId == user.Id).ExecuteDeleteAsync();

            Assert.Equal(expected, await OriginsAsync(user));
        }

        private async Task<Dictionary<string, TransactionOrigin>> OriginsAsync(Domain.Users.User user)
        {
            await using var context = Fixture.CreateContext();
            var rows = await new TransactionRepository(context)
                .StreamForExportAsync(user.Id!, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), TransactionTypeFilter.All, default)
                .ToListAsync();

            return rows.ToDictionary(row => row.Description, row => row.Origin);
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
