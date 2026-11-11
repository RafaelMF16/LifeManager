using LifeManager.Domain.Users;
using LifeManager.Infrastructure.Test.Seed;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Test.Users
{
    /// <summary>
    /// Deleting a user's row removes everything they own through the foreign keys (the base the account deletion of
    /// account-settings relies on). The check is table by table over the whole schema, so a table added later without
    /// its cascade shows up here.
    /// </summary>
    public class UserDeletionTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        [Fact]
        public async Task DeletingUser_ShouldRemoveEverythingTheyOwnAndKeepOtherUsers_WhenTheUserHasDataInEveryModule()
        {
            await using (var context = Fixture.CreateContext())
                await context.AddUserWithEverythingAsync("Bia");
            var countsBefore = await CountRowsPerTableAsync();

            User user;
            await using (var context = Fixture.CreateContext())
                user = await context.AddUserWithEverythingAsync("Ana");
            Assert.NotEqual(countsBefore, await CountRowsPerTableAsync());

            await using (var context = Fixture.CreateContext())
                await context.Users.Where(stored => stored.Id == user.Id).ExecuteDeleteAsync();

            Assert.Equal(countsBefore, await CountRowsPerTableAsync());
        }

        private async Task<Dictionary<string, long>> CountRowsPerTableAsync()
        {
            await using var context = Fixture.CreateContext();
            var tables = await context.Database
                .SqlQueryRaw<string>("""SELECT tablename AS "Value" FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory' ORDER BY tablename""")
                .ToListAsync();

            var counts = new Dictionary<string, long>();
            foreach (var table in tables)
            {
                // The name comes from the catalog, never from input; an identifier can't be a SQL parameter.
                var sql = "SELECT count(*) AS \"Value\" FROM \"" + table + "\"";
                counts[table] = await context.Database.SqlQueryRaw<long>(sql).SingleAsync();
            }

            return counts;
        }
    }
}
