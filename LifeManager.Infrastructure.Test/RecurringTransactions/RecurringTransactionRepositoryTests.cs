using LifeManager.Domain.RecurringTransactions;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Transactions;
using LifeManager.Infrastructure.RecurringTransactions;
using LifeManager.Infrastructure.Test.Seed;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Test.RecurringTransactions
{
    public class RecurringTransactionRepositoryTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        private static readonly DateOnly Today = new(2026, 11, 10);

        [Fact]
        public async Task TryPostOccurrenceAsync_ShouldPostOnce_WhenTwoRunsPostTheSameOccurrenceConcurrently()
        {
            await using var seedContext = Fixture.CreateContext();
            var user = await seedContext.AddUserAsync();
            var category = await seedContext.AddCategoryAsync(user, "Aluguel");
            var month = await seedContext.AddMonthAsync(user, 2026, 11);
            var recurrence = RecurringTransaction
                .Create(user.Id!.Value, MoneyFlowType.Expense, category.Id!.Value, 1500m, "Aluguel", 5, "2026-11", null, Today)
                .Unwrap();
            var stored = await new RecurringTransactionRepository(seedContext).AddAsync(recurrence, default);

            // Each run reads the recurrence on its own, like two job instances, and posts the same cursor month.
            var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            {
                await using var context = Fixture.CreateContext();
                var repository = new RecurringTransactionRepository(context);
                var loaded = (await repository.GetForPostingAsync(stored.Id!, default))!;
                var transaction = Transaction.CreateFromRecurrence(loaded, month).Unwrap();
                var expectedNextMonth = loaded.NextMonth;
                loaded.AdvanceAfterPosting();

                return await repository.TryPostOccurrenceAsync(loaded, expectedNextMonth, transaction, default);
            }));

            Assert.Equal(1, results.Count(posted => posted));
            Assert.Equal(1, await seedContext.Transactions.CountAsync(transaction => transaction.RecurringTransactionId == stored.Id));

            var cursor = await seedContext.RecurringTransactions.AsNoTracking().SingleAsync();
            Assert.Equal(TestData.Month(2026, 12), cursor.NextMonth);

            var totals = await seedContext.MonthlySummaries.AsNoTracking().SingleAsync();
            Assert.Equal(1500m, totals.TotalExpense.Value);
        }
    }
}
