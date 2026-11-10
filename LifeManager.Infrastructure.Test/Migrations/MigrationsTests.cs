using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Test.Migrations
{
    public class MigrationsTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        [Fact]
        public async Task MigrateAsync_ShouldApplyEveryMigration_WhenDatabaseStartsEmpty()
        {
            await using var context = Fixture.CreateContext();

            var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
            var pending = await context.Database.GetPendingMigrationsAsync();

            Assert.Empty(pending);
            Assert.Equal(context.Database.GetMigrations().Last(), applied.Last());
        }

        [Fact]
        public async Task Model_ShouldHaveNoChangesWithoutMigration_WhenComparedWithSnapshot()
        {
            await using var context = Fixture.CreateContext();

            Assert.False(context.Database.HasPendingModelChanges());
        }
    }
}
