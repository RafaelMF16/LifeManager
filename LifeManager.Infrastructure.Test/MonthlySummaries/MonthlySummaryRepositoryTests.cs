using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Infrastructure.MonthlySummaries;
using LifeManager.Infrastructure.Test.Seed;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Test.MonthlySummaries
{
    public class MonthlySummaryRepositoryTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        [Fact]
        public async Task GetNeighborsAsync_ShouldCrossTheTurnOfTheYear_WhenClosestMonthsAreInOtherYears()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var otherUser = await context.AddUserAsync("Bia");
            var october = await context.AddMonthAsync(user, 2025, 10);
            var december = await context.AddMonthAsync(user, 2025, 12);
            var january = await context.AddMonthAsync(user, 2026, 1);
            var march = await context.AddMonthAsync(user, 2026, 3);
            await context.AddMonthAsync(otherUser, 2026, 2);
            var repository = new MonthlySummaryRepository(context);

            var ofJanuary = await repository.GetNeighborsAsync(user.Id!, january.Year, january.Month, default);
            var ofDecember = await repository.GetNeighborsAsync(user.Id!, december.Year, december.Month, default);

            Assert.Equal(new MonthlySummaryNeighbors(december.Id!.Value, march.Id!.Value), ofJanuary);
            Assert.Equal(new MonthlySummaryNeighbors(october.Id!.Value, january.Id!.Value), ofDecember);
        }

        [Fact]
        public async Task GetNeighborsAsync_ShouldReturnNoNeighbors_WhenTheUserHasOnlyThatMonth()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var only = await context.AddMonthAsync(user, 2026, 11);

            var neighbors = await new MonthlySummaryRepository(context).GetNeighborsAsync(user.Id!, only.Year, only.Month, default);

            Assert.Equal(new MonthlySummaryNeighbors(null, null), neighbors);
        }

        [Fact]
        public async Task AddIfMissingAsync_ShouldReturnTheStoredMonth_WhenTheMonthAlreadyExists()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var existing = await context.AddMonthAsync(user, 2026, 11);

            var returned = await new MonthlySummaryRepository(context)
                .AddIfMissingAsync(MonthlySummary.OpenForImport(user.Id!, TestData.Month(2026, 11)), default);

            Assert.Equal(existing.Id, returned.Id);
            Assert.Equal(1, await context.MonthlySummaries.CountAsync());
        }

        [Fact]
        public async Task AddIfMissingAsync_ShouldStoreOneMonth_WhenTenCallsForTheSameMonthRunConcurrently()
        {
            await using var seedContext = Fixture.CreateContext();
            var user = await seedContext.AddUserAsync();

            var returnedIds = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
            {
                await using var context = Fixture.CreateContext();
                var month = await new MonthlySummaryRepository(context)
                    .AddIfMissingAsync(MonthlySummary.OpenForImport(user.Id!, TestData.Month(2024, 12)), default);

                return month.Id;
            }));

            Assert.Single(returnedIds.Distinct());
            Assert.Equal(1, await seedContext.MonthlySummaries.CountAsync());
            var stored = await seedContext.MonthlySummaries.AsNoTracking().SingleAsync();
            Assert.Equal(MonthlySummaryYear.FromPersistence(2024), stored.Year);
            Assert.Equal(0m, stored.BalanceAmount);
        }
    }
}
