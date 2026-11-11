using LifeManager.Domain.SavingsGoals;
using LifeManager.Domain.SavingsGoals.Enums;
using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Transactions;
using LifeManager.Infrastructure.SavingsGoals;
using LifeManager.Infrastructure.Transactions;
using LifeManager.Infrastructure.Test.Seed;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LifeManager.Infrastructure.Test.SavingsGoals
{
    public class SavingsGoalRepositoryTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        [Fact]
        public async Task AddAsync_ShouldStoreEveryField()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var goal = SavingsGoal.Create(user.Id!.Value, "Viagem ao Japão", 5000m, TestData.Month(2027, 12), 300.5m, "plane", TestData.Month(2026, 11), TestData.Now).Unwrap();

            await new SavingsGoalRepository(context).AddAsync(goal, default);

            await using var readContext = Fixture.CreateContext();
            var stored = await new SavingsGoalRepository(readContext).GetByIdAsync(goal.Id!, user.Id!, default);
            Assert.NotNull(stored);
            Assert.Equal("Viagem ao Japão", stored.Name.Value);
            Assert.Equal("viagem ao japao", stored.NormalizedName);
            Assert.Equal(5000m, stored.TargetAmount);
            Assert.Equal(TestData.Month(2027, 12), stored.TargetMonth);
            Assert.Equal(300.5m, stored.InitialAmount);
            Assert.Equal("plane", stored.Icon);
            Assert.Null(stored.ArchivedAt);
            Assert.Null(stored.CompletedAt);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNull_ForAnotherUsersGoal()
        {
            await using var context = Fixture.CreateContext();
            var owner = await context.AddUserAsync();
            var other = await context.AddUserAsync("Bia");
            var goal = await context.AddSavingsGoalAsync(owner);

            Assert.Null(await new SavingsGoalRepository(context).GetByIdAsync(goal.Id!, other.Id!, default));
        }

        [Fact]
        public async Task ExistsActiveByNameAsync_ShouldIgnoreArchivedGoalsAndTheGivenId()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var archived = await context.AddSavingsGoalAsync(user, "Viagem");
            archived.Archive(TestData.Now).Unwrap();
            await context.SaveChangesAsync();
            var active = await context.AddSavingsGoalAsync(user, "Reserva");
            var repository = new SavingsGoalRepository(context);

            Assert.False(await repository.ExistsActiveByNameAsync(user.Id!, SavingsGoalName.Create("VIAGEM").Unwrap(), null, default));
            Assert.True(await repository.ExistsActiveByNameAsync(user.Id!, SavingsGoalName.Create("reserva").Unwrap(), null, default));
            Assert.False(await repository.ExistsActiveByNameAsync(user.Id!, SavingsGoalName.Create("reserva").Unwrap(), active.Id, default));
        }

        [Fact]
        public async Task UniqueIndex_ShouldRefuseTwoActiveGoalsWithTheSameName_ButAllowAnArchivedOne()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var first = await context.AddSavingsGoalAsync(user, "Viagem");
            first.Archive(TestData.Now).Unwrap();
            await context.SaveChangesAsync();
            await context.AddSavingsGoalAsync(user, "Viagem");

            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.AddSavingsGoalAsync(user, "viagem"));

            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        }

        [Fact]
        public async Task GetPagedByUserIdAsync_ShouldSearchWithAnEscapedLike_AndSortTargetMonthWithNullsLast()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var current = TestData.Month(2026, 11);
            foreach (var (name, month) in new[] { ("Viagem 100%", (LifeManager.Domain.Shared.ValueObjects.YearMonth?)TestData.Month(2028, 1)), ("Viagem 100x", TestData.Month(2027, 1)), ("Sem prazo", null) })
                context.Add(SavingsGoal.Create(user.Id!.Value, name, 1000m, month, 0m, null, current, TestData.Now).Unwrap());
            await context.SaveChangesAsync();
            var repository = new SavingsGoalRepository(context);
            var page = PageRequest.Create(1, 20).Unwrap();

            var searched = await repository.GetPagedByUserIdAsync(user.Id!, page, SavingsGoalStatusFilter.Active, "100%", SavingsGoalSortBy.Name, SortDirection.Asc, default);
            var ascending = await repository.GetPagedByUserIdAsync(user.Id!, page, SavingsGoalStatusFilter.Active, null, SavingsGoalSortBy.TargetMonth, SortDirection.Asc, default);
            var descending = await repository.GetPagedByUserIdAsync(user.Id!, page, SavingsGoalStatusFilter.Active, null, SavingsGoalSortBy.TargetMonth, SortDirection.Desc, default);

            Assert.Equal("Viagem 100%", Assert.Single(searched.Items).Name.Value);
            Assert.Equal(["Viagem 100x", "Viagem 100%", "Sem prazo"], ascending.Items.Select(goal => goal.Name.Value));
            Assert.Equal(["Sem prazo", "Viagem 100%", "Viagem 100x"], descending.Items.Select(goal => goal.Name.Value));
        }

        [Fact]
        public async Task UpdateAsync_ShouldSaveTheFieldsAndArchiving()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var goal = await context.AddSavingsGoalAsync(user);
            goal.Update("Japão", 8000m, TestData.Month(2027, 6), 50m, "globe", TestData.Month(2026, 11)).Unwrap();
            goal.Archive(TestData.Now).Unwrap();

            await using (var writeContext = Fixture.CreateContext())
                await new SavingsGoalRepository(writeContext).UpdateAsync(goal, default);

            await using var readContext = Fixture.CreateContext();
            var stored = await new SavingsGoalRepository(readContext).GetByIdAsync(goal.Id!, user.Id!, default);
            Assert.NotNull(stored);
            Assert.Equal("japao", stored.NormalizedName);
            Assert.Equal(8000m, stored.TargetAmount);
            Assert.Equal(TestData.Month(2027, 6), stored.TargetMonth);
            Assert.Equal(50m, stored.InitialAmount);
            Assert.Equal("globe", stored.Icon);
            Assert.NotNull(stored.ArchivedAt);
        }

        private static async Task LinkAsync(TransactionRepository repository, Domain.MonthlySummaries.MonthlySummary month, Domain.Categories.Category category, SavingsGoal? goal, decimal amount, int day)
        {
            var date = new DateOnly(month.Year.Value, month.Month.Value, day);
            await repository.AddAsync(Transaction.Create(MoneyFlowType.Investment, category.Id!.Value, amount, "Aporte", date, month, goal?.Id!.Value).Unwrap(), default);
        }

        [Fact]
        public async Task GetLinkedSumsAsync_ShouldGroupTheInvestmentsByGoal_WithTheRecentPart()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var category = await context.AddCategoryAsync(user);
            var august = await context.AddMonthAsync(user, 2026, 8);
            var november = await context.AddMonthAsync(user, 2026, 11);
            var travel = await context.AddSavingsGoalAsync(user, "Viagem");
            var reserve = await context.AddSavingsGoalAsync(user, "Reserva");
            var empty = await context.AddSavingsGoalAsync(user, "Vazio");
            var transactions = new TransactionRepository(context);
            await LinkAsync(transactions, august, category, travel, 100m, 5);
            await LinkAsync(transactions, november, category, travel, 250.5m, 3);
            await LinkAsync(transactions, november, category, reserve, 40m, 3);
            await LinkAsync(transactions, november, category, null, 999m, 3);

            var sums = await new SavingsGoalRepository(context).GetLinkedSumsAsync([travel.Id!, reserve.Id!, empty.Id!], new DateOnly(2026, 9, 1), default);

            Assert.Equal(new SavingsGoalSums(350.5m, 250.5m), sums[travel.Id!]);
            Assert.Equal(new SavingsGoalSums(40m, 40m), sums[reserve.Id!]);
            Assert.False(sums.ContainsKey(empty.Id!));
        }

        [Fact]
        public async Task GetMonthlyAmountsAsync_ShouldSumPerMonthFromTheGivenDay()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var category = await context.AddCategoryAsync(user);
            var october = await context.AddMonthAsync(user, 2026, 10);
            var november = await context.AddMonthAsync(user, 2026, 11);
            var goal = await context.AddSavingsGoalAsync(user);
            var transactions = new TransactionRepository(context);
            await LinkAsync(transactions, october, category, goal, 10m, 1);
            await LinkAsync(transactions, october, category, goal, 20m, 31);
            await LinkAsync(transactions, november, category, goal, 5m, 2);

            var months = await new SavingsGoalRepository(context).GetMonthlyAmountsAsync(goal.Id!, new DateOnly(2026, 10, 2), default);

            Assert.Equal(
                [new SavingsGoalMonthAmount(TestData.Month(2026, 10), 20m), new SavingsGoalMonthAmount(TestData.Month(2026, 11), 5m)],
                months);
        }

        [Fact]
        public async Task GetPagedByUserIdAsync_ShouldSortByProgressInSql()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var category = await context.AddCategoryAsync(user);
            var month = await context.AddMonthAsync(user, 2026, 11);
            var half = await context.AddSavingsGoalAsync(user, "Metade", 1000m);
            var tenth = await context.AddSavingsGoalAsync(user, "Um décimo", 10000m);
            await context.AddSavingsGoalAsync(user, "Nada", 50m);
            var transactions = new TransactionRepository(context);
            await LinkAsync(transactions, month, category, half, 500m, 1);
            await LinkAsync(transactions, month, category, tenth, 1000m, 1);
            var page = PageRequest.Create(1, 20).Unwrap();

            var ascending = await new SavingsGoalRepository(context).GetPagedByUserIdAsync(
                user.Id!, page, SavingsGoalStatusFilter.Active, null, SavingsGoalSortBy.Progress, SortDirection.Asc, default);

            Assert.Equal(["Nada", "Um décimo", "Metade"], ascending.Items.Select(goal => goal.Name.Value));
        }
    }
}
