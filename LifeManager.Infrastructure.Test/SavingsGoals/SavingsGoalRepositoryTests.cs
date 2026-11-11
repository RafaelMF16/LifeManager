using LifeManager.Domain.SavingsGoals;
using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Infrastructure.SavingsGoals;
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
    }
}
