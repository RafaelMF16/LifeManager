using LifeManager.Domain.Shared.Enums;
using LifeManager.Infrastructure.Categories;
using LifeManager.Infrastructure.Test.Seed;
using LifeManager.Infrastructure.Transactions;
using Npgsql;

namespace LifeManager.Infrastructure.Test.Categories
{
    public class CategoryRepositoryTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        [Fact]
        public async Task DeleteAsync_ShouldBeRefusedByTheForeignKey_WhenATransactionStillUsesTheCategory()
        {
            // CategoryService refuses this first (Category.InUse); the deferred foreign key is the last guard, now at the commit.
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var category = await context.AddCategoryAsync(user);
            var month = await context.AddMonthAsync(user, 2026, 11);
            await new TransactionRepository(context).AddAsync(TestData.NewTransaction(month, category, MoneyFlowType.Expense, 10m), default);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => new CategoryRepository(context).DeleteAsync(category.Id!, user.Id!, default));

            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
        }
    }
}
