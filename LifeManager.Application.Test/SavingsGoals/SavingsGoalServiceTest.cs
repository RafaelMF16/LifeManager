using LifeManager.Application.SavingsGoals.DTOs;
using LifeManager.Application.SavingsGoals.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.SavingsGoals;
using LifeManager.Domain.SavingsGoals.Errors;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.SavingsGoals
{
    [Collection("ApplicationServices")]
    public class SavingsGoalServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly UserId OtherUserId = new(2);
        private static readonly DateOnly Today = new(2026, 11, 11);

        private readonly SavingsGoalService _service;

        public SavingsGoalServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<SavingsGoalService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);

            SavingsGoalSingleton.Instance.Clear();
        }

        private static SavingsGoal SeedGoal(string name, UserId? userId = null, bool archived = false, decimal target = 1000m, DateOnly? targetMonthStart = null)
        {
            var goal = SavingsGoal.FromPersistence(
                SavingsGoalSingleton.Instance.Count + 1,
                (userId ?? UserId).Value,
                name,
                target,
                targetMonthStart,
                0m,
                null,
                new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
                archived ? new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero) : null,
                completedAt: null);
            SavingsGoalSingleton.Instance.Add(goal);

            return goal;
        }

        [Fact]
        public async Task CreateAsync_ShouldStoreTheGoal()
        {
            var result = await _service.CreateAsync(new SavingsGoalDto(" Viagem ", 5000m, "2027-12", 300m, "plane"), UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var goal = result.Value!;
            Assert.Equal("Viagem", goal.Name);
            Assert.Equal(5000m, goal.TargetAmount);
            Assert.Equal("2027-12", goal.TargetMonth);
            Assert.Equal(300m, goal.InitialAmount);
            Assert.Equal("plane", goal.Icon);
            Assert.Null(goal.ArchivedAt);
            Assert.Null(goal.CompletedAt);
            Assert.Single(SavingsGoalSingleton.Instance);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task CreateAsync_ShouldStoreAGoalWithoutDeadline(string? targetMonth)
        {
            var result = await _service.CreateAsync(new SavingsGoalDto("Reserva", 20000m, targetMonth), UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value!.TargetMonth);
        }

        [Theory]
        [InlineData("2027-13")]
        [InlineData("dez/2027")]
        public async Task CreateAsync_ShouldReturnInvalidTargetMonth_WhenItIsNotAMonth(string targetMonth)
        {
            var result = await _service.CreateAsync(new SavingsGoalDto("Viagem", 5000m, targetMonth), UserId, CancellationToken.None);

            Assert.Equal(SavingsGoalErrors.InvalidTargetMonth, result.Error);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnTargetMonthInPast_WhenTheDeadlineIsBeforeTheCurrentMonth()
        {
            var result = await _service.CreateAsync(new SavingsGoalDto("Viagem", 5000m, "2026-10"), UserId, CancellationToken.None);

            Assert.Equal(SavingsGoalErrors.TargetMonthInPast, result.Error);
            Assert.Empty(SavingsGoalSingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldAcceptTheCurrentMonthAsDeadline()
        {
            var result = await _service.CreateAsync(new SavingsGoalDto("Presente", 200m, "2026-11"), UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnNameAlreadyExists_WhenAnActiveGoalHasTheNameIgnoringCaseAndAccents()
        {
            SeedGoal("Reserva de Emergência");

            var result = await _service.CreateAsync(new SavingsGoalDto("reserva de emergencia", 1000m), UserId, CancellationToken.None);

            Assert.Equal(SavingsGoalErrors.NameAlreadyExists, result.Error);
        }

        [Fact]
        public async Task CreateAsync_ShouldAcceptTheName_WhenOnlyAnArchivedGoalOrAnotherUserHasIt()
        {
            SeedGoal("Viagem", archived: true);
            SeedGoal("Viagem", OtherUserId);

            var result = await _service.CreateAsync(new SavingsGoalDto("Viagem", 1000m), UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNotFound_WhenTheGoalIsAnotherUsers()
        {
            var goal = SeedGoal("Viagem", OtherUserId);

            var result = await _service.GetByIdAsync(goal.Id!.Value, UserId, CancellationToken.None);

            Assert.Equal(SavingsGoalErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnTheGoal_EvenArchived()
        {
            var goal = SeedGoal("Viagem", archived: true, targetMonthStart: new DateOnly(2020, 1, 1));

            var result = await _service.GetByIdAsync(goal.Id!.Value, UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("2020-01", result.Value!.TargetMonth);
            Assert.NotNull(result.Value.ArchivedAt);
        }
    }
}
