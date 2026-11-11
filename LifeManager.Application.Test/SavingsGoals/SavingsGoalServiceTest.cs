using LifeManager.Application.SavingsGoals.DTOs;
using LifeManager.Application.SavingsGoals.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.SavingsGoals;
using LifeManager.Domain.SavingsGoals.Enums;
using LifeManager.Domain.SavingsGoals.Errors;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
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

        private static SavingsGoal SeedGoal(
            string name, UserId? userId = null, bool archived = false, decimal target = 1000m, DateOnly? targetMonthStart = null, DateOnly? completedAt = null)
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
                completedAt);
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

        [Fact]
        public async Task GetPagedAsync_ShouldSearchIgnoringAccentsAndCase()
        {
            SeedGoal("Reserva de Emergência");
            SeedGoal("Viagem");

            var result = await _service.GetPagedAsync(new SavingsGoalListQueryDto { Search = "EMERGENCIA" }, UserId, CancellationToken.None);

            Assert.Equal("Reserva de Emergência", Assert.Single(result.Value!.Items).Name);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldSplitActiveCompletedAndArchived()
        {
            SeedGoal("Ativo");
            SeedGoal("Concluído", completedAt: new DateOnly(2026, 10, 1));
            SeedGoal("Arquivado", archived: true, completedAt: new DateOnly(2026, 10, 1));
            SeedGoal("De outro", OtherUserId);

            async Task<string> Single(SavingsGoalStatusFilter status)
                => Assert.Single((await _service.GetPagedAsync(new SavingsGoalListQueryDto { Status = status }, UserId, CancellationToken.None)).Value!.Items).Name;

            Assert.Equal("Ativo", await Single(SavingsGoalStatusFilter.Active));
            Assert.Equal("Concluído", await Single(SavingsGoalStatusFilter.Completed));
            Assert.Equal("Arquivado", await Single(SavingsGoalStatusFilter.Archived));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldSortByTargetMonth_WithoutDeadlineLastAscending()
        {
            SeedGoal("Sem prazo");
            SeedGoal("Longe", targetMonthStart: new DateOnly(2028, 1, 1));
            SeedGoal("Perto", targetMonthStart: new DateOnly(2027, 1, 1));

            var ascending = await _service.GetPagedAsync(new SavingsGoalListQueryDto { SortBy = SavingsGoalSortBy.TargetMonth }, UserId, CancellationToken.None);
            var descending = await _service.GetPagedAsync(
                new SavingsGoalListQueryDto { SortBy = SavingsGoalSortBy.TargetMonth, SortDirection = SortDirection.Desc }, UserId, CancellationToken.None);

            Assert.Equal(["Perto", "Longe", "Sem prazo"], ascending.Value!.Items.Select(goal => goal.Name));
            Assert.Equal(["Sem prazo", "Longe", "Perto"], descending.Value!.Items.Select(goal => goal.Name));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldPageByName()
        {
            foreach (var name in new[] { "C", "A", "B" })
                SeedGoal(name);

            var secondPage = await _service.GetPagedAsync(new SavingsGoalListQueryDto { Page = 2, PageSize = 2 }, UserId, CancellationToken.None);
            var beyond = await _service.GetPagedAsync(new SavingsGoalListQueryDto { Page = 5, PageSize = 2 }, UserId, CancellationToken.None);
            var descending = await _service.GetPagedAsync(new SavingsGoalListQueryDto { SortDirection = SortDirection.Desc }, UserId, CancellationToken.None);

            Assert.Equal("C", Assert.Single(secondPage.Value!.Items).Name);
            Assert.Equal(3, secondPage.Value.TotalCount);
            Assert.Equal(2, secondPage.Value.TotalPages);
            Assert.Empty(beyond.Value!.Items);
            Assert.Equal(["C", "B", "A"], descending.Value!.Items.Select(goal => goal.Name));
        }

        [Theory]
        [InlineData(0, 20)]
        [InlineData(1, 0)]
        [InlineData(1, 101)]
        public async Task GetPagedAsync_ShouldReturnPagingErrors_WhenThePageIsInvalid(int page, int pageSize)
        {
            var result = await _service.GetPagedAsync(new SavingsGoalListQueryDto { Page = page, PageSize = pageSize }, UserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Error, new[] { PagingErrors.InvalidPage, PagingErrors.InvalidPageSize });
        }

        [Fact]
        public async Task UpdateAsync_ShouldChangeEveryField()
        {
            var goal = SeedGoal("Viagem");

            var result = await _service.UpdateAsync(goal.Id!.Value, new SavingsGoalDto("Japão", 8000m, "2027-06", 500m, "globe"), UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var stored = Assert.Single(SavingsGoalSingleton.Instance);
            Assert.Equal("Japão", stored.Name.Value);
            Assert.Equal(8000m, stored.TargetAmount);
            Assert.Equal(new DateOnly(2027, 6, 1), stored.TargetMonthStart);
            Assert.Equal(500m, stored.InitialAmount);
            Assert.Equal("globe", stored.Icon);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNameAlreadyExists_WhenRenamingToAnotherActiveGoal()
        {
            SeedGoal("Reserva");
            var goal = SeedGoal("Viagem");

            var result = await _service.UpdateAsync(goal.Id!.Value, new SavingsGoalDto("RESERVA", 1000m), UserId, CancellationToken.None);

            Assert.Equal(SavingsGoalErrors.NameAlreadyExists, result.Error);
        }

        [Fact]
        public async Task UpdateAsync_ShouldAllowADifferentCasingOfItsOwnName()
        {
            var goal = SeedGoal("viagem");

            var result = await _service.UpdateAsync(goal.Id!.Value, new SavingsGoalDto("Viagem", 1000m), UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnTargetMonthInPast_AndArchived()
        {
            var goal = SeedGoal("Viagem");
            var archived = SeedGoal("Velho", archived: true);

            var pastResult = await _service.UpdateAsync(goal.Id!.Value, new SavingsGoalDto("Viagem", 1000m, "2026-10"), UserId, CancellationToken.None);
            var archivedResult = await _service.UpdateAsync(archived.Id!.Value, new SavingsGoalDto("Velho", 1000m), UserId, CancellationToken.None);

            Assert.Equal(SavingsGoalErrors.TargetMonthInPast, pastResult.Error);
            Assert.Equal(SavingsGoalErrors.Archived, archivedResult.Error);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNotFound_ForAnotherUsersGoal()
        {
            var goal = SeedGoal("Viagem", OtherUserId);

            var result = await _service.UpdateAsync(goal.Id!.Value, new SavingsGoalDto("Viagem", 1000m), UserId, CancellationToken.None);

            Assert.Equal(SavingsGoalErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task ArchiveAndRestore_ShouldFreeAndTakeBackTheName()
        {
            var goal = SeedGoal("Viagem");

            Assert.True((await _service.ArchiveAsync(goal.Id!.Value, UserId, CancellationToken.None)).IsSuccess);
            Assert.Equal(SavingsGoalErrors.AlreadyArchived, (await _service.ArchiveAsync(goal.Id.Value, UserId, CancellationToken.None)).Error);
            Assert.True((await _service.CreateAsync(new SavingsGoalDto("viagem", 500m), UserId, CancellationToken.None)).IsSuccess);

            var restoreResult = await _service.RestoreAsync(goal.Id.Value, UserId, CancellationToken.None);

            Assert.Equal(SavingsGoalErrors.NameAlreadyExists, restoreResult.Error);
            Assert.True(SavingsGoalSingleton.Instance.Single(stored => stored.Id == goal.Id).IsArchived);
        }

        [Fact]
        public async Task RestoreAsync_ShouldReactivateTheGoal_AndRefuseAnActiveOne()
        {
            var goal = SeedGoal("Viagem", archived: true);

            var result = await _service.RestoreAsync(goal.Id!.Value, UserId, CancellationToken.None);
            var again = await _service.RestoreAsync(goal.Id.Value, UserId, CancellationToken.None);

            Assert.Null(result.Value!.ArchivedAt);
            Assert.Equal(SavingsGoalErrors.NotArchived, again.Error);
        }
    }
}
