using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Habits.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.Habits.Mocks;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Habits
{
    [Collection("ApplicationServices")]
    public class HabitServiceTest : BaseTest
    {
        private static readonly UserId FirstUserId = new(1);
        private static readonly UserId SecondUserId = new(2);
        private static readonly DateOnly Today = new(2026, 10, 7);

        private readonly HabitService _habitService;
        private readonly HabitRepositoryMock _habitRepository;
        private readonly FakeTimeProvider _timeProvider;

        public HabitServiceTest()
        {
            _habitService = ServiceProvider.GetRequiredService<HabitService>();
            _habitRepository = (HabitRepositoryMock)ServiceProvider.GetRequiredService<IHabitRepository>();
            _timeProvider = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();
            _timeProvider.SetToday(Today);

            HabitSingleton.Instance.Clear();
        }

        private static HabitDto Daily(string name = "Read", HabitKind kind = HabitKind.Positive)
            => new(name, null, null, kind, HabitDifficulty.Easy, HabitFrequencyType.Daily, null, null);

        private static HabitUpdateDto UpdateDaily(string name = "Read", HabitDifficulty difficulty = HabitDifficulty.Easy)
            => new(name, null, null, difficulty, HabitFrequencyType.Daily, null, null);

        private async Task<HabitResponseDto> CreateHabit(string name, UserId? userId = null)
        {
            var result = await _habitService.CreateAsync(Daily(name), userId ?? FirstUserId, CancellationToken.None);
            Assert.True(result.IsSuccess);
            return result.Value;
        }

        private async Task ArchiveHabit(int id, UserId? userId = null)
            => Assert.True((await _habitService.ArchiveAsync(id, userId ?? FirstUserId, CancellationToken.None)).IsSuccess);

        [Fact]
        public async Task CreateAsync_ShouldAddHabitStartingToday_WhenValuesAreValid()
        {
            var dto = new HabitDto(" Read ", "10 pages", "After dinner", HabitKind.Positive, HabitDifficulty.Hard, HabitFrequencyType.Daily, null, null);

            var result = await _habitService.CreateAsync(dto, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Read", result.Value.Name);
            Assert.Equal("10 pages", result.Value.Description);
            Assert.Equal("After dinner", result.Value.Trigger);
            Assert.Equal(HabitDifficulty.Hard, result.Value.Difficulty);
            Assert.Equal(Today, result.Value.StartDate);
            Assert.Null(result.Value.ArchivedAt);
            var habit = Assert.Single(HabitSingleton.Instance);
            Assert.Equal(result.Value.Id, habit.Id!.Value);
            Assert.Equal(FirstUserId, habit.UserId);
            Assert.Equal(Today.AddDays(-1), habit.EvaluatedUntil);
        }

        [Fact]
        public async Task CreateAsync_ShouldMapWeekDaysMondayFirst_WhenFrequencyIsWeekDays()
        {
            var dto = new HabitDto("Gym", null, null, HabitKind.Positive, HabitDifficulty.Medium, HabitFrequencyType.WeekDays,
                [DayOfWeek.Sunday, DayOfWeek.Wednesday, DayOfWeek.Monday], null);

            var result = await _habitService.CreateAsync(dto, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Sunday], result.Value.WeekDays);
            Assert.Equal(HabitWeekDays.Monday | HabitWeekDays.Wednesday | HabitWeekDays.Sunday, Assert.Single(HabitSingleton.Instance).WeekDays);
        }

        [Fact]
        public async Task CreateAsync_ShouldKeepTimesPerWeek_WhenFrequencyIsTimesPerWeek()
        {
            var dto = new HabitDto("Run", null, null, HabitKind.Positive, HabitDifficulty.Medium, HabitFrequencyType.TimesPerWeek, null, 3);

            var result = await _habitService.CreateAsync(dto, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.TimesPerWeek);
            Assert.Empty(result.Value.WeekDays);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnValidationError_WhenWeekDaysIsEmpty()
        {
            var dto = new HabitDto("Gym", null, null, HabitKind.Positive, HabitDifficulty.Medium, HabitFrequencyType.WeekDays, [], null);

            var result = await _habitService.CreateAsync(dto, FirstUserId, CancellationToken.None);

            Assert.Equal(HabitErrors.WeekDaysRequired, result.Error);
            Assert.Empty(HabitSingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnValidationError_WhenWeekDaysHasAnUndefinedDay()
        {
            var dto = new HabitDto("Gym", null, null, HabitKind.Positive, HabitDifficulty.Medium, HabitFrequencyType.WeekDays, [(DayOfWeek)9], null);

            var result = await _habitService.CreateAsync(dto, FirstUserId, CancellationToken.None);

            Assert.Equal(HabitErrors.InvalidFrequencyCombination, result.Error);
        }

        [Fact]
        public async Task CreateAsync_ShouldCreateNegativeHabitOnWeekDays_WhenItIsAvoidedOnlyOnSomeDays()
        {
            // Quit gaming on weekdays; the weekend stays free.
            var dto = new HabitDto("Video games", null, null, HabitKind.Negative, HabitDifficulty.Hard, HabitFrequencyType.WeekDays,
                [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday], null);

            var result = await _habitService.CreateAsync(dto, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(HabitKind.Negative, result.Value.Kind);
            Assert.Equal(5, result.Value.WeekDays.Count);
            var habit = Assert.Single(HabitSingleton.Instance);
            Assert.Equal(HabitWeekDays.Monday | HabitWeekDays.Tuesday | HabitWeekDays.Wednesday | HabitWeekDays.Thursday | HabitWeekDays.Friday, habit.WeekDays);
        }

        [Fact]
        public async Task CreateAsync_ShouldCreateNegativeHabitWithAWeeklyAllowance()
        {
            var dto = new HabitDto("Video games", null, null, HabitKind.Negative, HabitDifficulty.Medium, HabitFrequencyType.TimesPerWeek, null, 2);

            var result = await _habitService.CreateAsync(dto, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Value.TimesPerWeek);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnConflict_WhenActiveHabitHasSameNameIgnoringCaseAndAccents()
        {
            await CreateHabit("Meditação");

            var result = await _habitService.CreateAsync(Daily(" MEDITACAO "), FirstUserId, CancellationToken.None);

            Assert.Equal(HabitErrors.NameAlreadyExists, result.Error);
            Assert.Equal(ErrorType.Conflict, result.Error!.Type);
            Assert.Single(HabitSingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldAllowSameName_WhenTheOtherHabitIsArchived()
        {
            var archived = await CreateHabit("Read");
            await ArchiveHabit(archived.Id);

            var result = await _habitService.CreateAsync(Daily("Read"), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, HabitSingleton.Instance.Count);
        }

        [Fact]
        public async Task CreateAsync_ShouldAllowSameName_WhenTheOtherHabitBelongsToAnotherUser()
        {
            await CreateHabit("Read", SecondUserId);

            var result = await _habitService.CreateAsync(Daily("Read"), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnHabit_WhenItBelongsToTheUser()
        {
            var created = await CreateHabit("Read");

            var result = await _habitService.GetByIdAsync(created.Id, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(created.Id, result.Value.Id);
            Assert.Equal("Read", result.Value.Name);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNotFound_WhenItBelongsToAnotherUser()
        {
            var created = await CreateHabit("Read", SecondUserId);

            var result = await _habitService.GetByIdAsync(created.Id, FirstUserId, CancellationToken.None);

            Assert.Equal(HabitErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task UpdateAsync_ShouldChangeValues_WhenValuesAreValid()
        {
            var created = await CreateHabit("Read");
            var dto = new HabitUpdateDto("Read more", "desc", "After lunch", HabitDifficulty.Hard, HabitFrequencyType.WeekDays, [DayOfWeek.Friday], null);

            var result = await _habitService.UpdateAsync(created.Id, dto, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Read more", result.Value.Name);
            Assert.Equal([DayOfWeek.Friday], result.Value.WeekDays);
            var habit = Assert.Single(HabitSingleton.Instance);
            Assert.Equal("read more", habit.NormalizedName);
            Assert.Equal(HabitDifficulty.Hard, habit.Difficulty);
            Assert.Equal("After lunch", habit.Trigger!.Value);
        }

        [Fact]
        public async Task UpdateAsync_ShouldSkipNameCheck_WhenNormalizedNameIsUnchanged()
        {
            var created = await CreateHabit("Read");
            var callsBefore = _habitRepository.ExistsActiveByNameCallCount;

            var result = await _habitService.UpdateAsync(created.Id, UpdateDaily("READ", HabitDifficulty.Hard), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(callsBefore, _habitRepository.ExistsActiveByNameCallCount);
            Assert.Equal("READ", Assert.Single(HabitSingleton.Instance).Name.Value);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnConflict_WhenAnotherActiveHabitHasTheName()
        {
            await CreateHabit("Read");
            var other = await CreateHabit("Run");

            var result = await _habitService.UpdateAsync(other.Id, UpdateDaily("réad"), FirstUserId, CancellationToken.None);

            Assert.Equal(HabitErrors.NameAlreadyExists, result.Error);
            Assert.Equal(0, _habitRepository.UpdateCallCount);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnConflict_WhenHabitIsArchived()
        {
            var created = await CreateHabit("Read");
            await ArchiveHabit(created.Id);

            var result = await _habitService.UpdateAsync(created.Id, UpdateDaily("Other"), FirstUserId, CancellationToken.None);

            Assert.Equal(HabitErrors.Archived, result.Error);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNotFound_WhenHabitBelongsToAnotherUser()
        {
            var created = await CreateHabit("Read", SecondUserId);

            var result = await _habitService.UpdateAsync(created.Id, UpdateDaily("Other"), FirstUserId, CancellationToken.None);

            Assert.Equal(HabitErrors.NotFound, result.Error);
            Assert.Equal("Read", Assert.Single(HabitSingleton.Instance).Name.Value);
        }

        [Fact]
        public async Task ArchiveAsync_ShouldSetArchivedAtToNow()
        {
            var created = await CreateHabit("Read");

            var result = await _habitService.ArchiveAsync(created.Id, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(_timeProvider.UtcNow, Assert.Single(HabitSingleton.Instance).ArchivedAt);
        }

        [Fact]
        public async Task ArchiveAsync_ShouldReturnConflict_WhenAlreadyArchived()
        {
            var created = await CreateHabit("Read");
            await ArchiveHabit(created.Id);

            var result = await _habitService.ArchiveAsync(created.Id, FirstUserId, CancellationToken.None);

            Assert.Equal(HabitErrors.AlreadyArchived, result.Error);
        }

        [Fact]
        public async Task ArchiveAsync_ShouldReturnNotFound_WhenHabitBelongsToAnotherUser()
        {
            var created = await CreateHabit("Read", SecondUserId);

            var result = await _habitService.ArchiveAsync(created.Id, FirstUserId, CancellationToken.None);

            Assert.Equal(HabitErrors.NotFound, result.Error);
            Assert.False(Assert.Single(HabitSingleton.Instance).IsArchived);
        }

        [Fact]
        public async Task RestoreAsync_ShouldReactivateHabitFromToday()
        {
            var created = await CreateHabit("Read");
            await ArchiveHabit(created.Id);
            var restoreDay = Today.AddDays(10);
            _timeProvider.SetToday(restoreDay);

            var result = await _habitService.RestoreAsync(created.Id, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.ArchivedAt);
            var habit = Assert.Single(HabitSingleton.Instance);
            Assert.False(habit.IsArchived);
            Assert.Equal(restoreDay.AddDays(-1), habit.EvaluatedUntil);
        }

        [Fact]
        public async Task RestoreAsync_ShouldReturnConflict_WhenAnActiveHabitTookTheName()
        {
            var archived = await CreateHabit("Read");
            await ArchiveHabit(archived.Id);
            await CreateHabit("Read");

            var result = await _habitService.RestoreAsync(archived.Id, FirstUserId, CancellationToken.None);

            Assert.Equal(HabitErrors.NameAlreadyExists, result.Error);
            Assert.True(HabitSingleton.Instance.Single(habit => habit.Id!.Value == archived.Id).IsArchived);
        }

        [Fact]
        public async Task RestoreAsync_ShouldReturnConflict_WhenHabitIsNotArchived()
        {
            var created = await CreateHabit("Read");

            var result = await _habitService.RestoreAsync(created.Id, FirstUserId, CancellationToken.None);

            Assert.Equal(HabitErrors.NotArchived, result.Error);
        }

        [Fact]
        public async Task RestoreAsync_ShouldReturnNotFound_WhenHabitDoesNotExist()
        {
            var result = await _habitService.RestoreAsync(99, FirstUserId, CancellationToken.None);

            Assert.Equal(HabitErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnOnlyTheUsersActiveHabitsSortedByName_ByDefault()
        {
            await CreateHabit("Run");
            await CreateHabit("Água");
            var archived = await CreateHabit("Meditate");
            await ArchiveHabit(archived.Id);
            await CreateHabit("Other user", SecondUserId);

            var result = await _habitService.GetPagedAsync(new HabitListQueryDto(), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(["Água", "Run"], result.Value.Items.Select(habit => habit.Name));
            Assert.Equal(2, result.Value.TotalCount);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnArchivedHabits_WhenStatusIsArchived()
        {
            await CreateHabit("Run");
            var archived = await CreateHabit("Meditate");
            await ArchiveHabit(archived.Id);

            var result = await _habitService.GetPagedAsync(new HabitListQueryDto { Status = HabitStatusFilter.Archived }, FirstUserId, CancellationToken.None);

            var item = Assert.Single(result.Value!.Items);
            Assert.Equal("Meditate", item.Name);
            Assert.NotNull(item.ArchivedAt);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldSortDescending_WhenSortDirectionIsDesc()
        {
            await CreateHabit("A");
            await CreateHabit("C");
            await CreateHabit("B");

            var result = await _habitService.GetPagedAsync(new HabitListQueryDto { SortDirection = SortDirection.Desc }, FirstUserId, CancellationToken.None);

            Assert.Equal(["C", "B", "A"], result.Value!.Items.Select(habit => habit.Name));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldSortByCreationTime_WhenSortByIsCreatedAt()
        {
            await CreateHabit("B");
            _timeProvider.UtcNow = _timeProvider.UtcNow.AddMinutes(1);
            await CreateHabit("A");

            var result = await _habitService.GetPagedAsync(new HabitListQueryDto { SortBy = HabitSortBy.CreatedAt }, FirstUserId, CancellationToken.None);

            Assert.Equal(["B", "A"], result.Value!.Items.Select(habit => habit.Name));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldMatchIgnoringCaseAndAccents_WhenSearching()
        {
            await CreateHabit("Meditação");
            await CreateHabit("Run");

            var result = await _habitService.GetPagedAsync(new HabitListQueryDto { Search = "MEDITA" }, FirstUserId, CancellationToken.None);

            Assert.Equal("Meditação", Assert.Single(result.Value!.Items).Name);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnEmptyPage_WhenSearchIsLongerThanAName()
        {
            await CreateHabit("Read");

            var result = await _habitService.GetPagedAsync(new HabitListQueryDto { Search = new string('a', 61) }, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value.Items);
            Assert.Equal(0, result.Value.TotalCount);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnRequestedPage_WhenThereAreMoreHabitsThanPageSize()
        {
            foreach (var name in new[] { "A", "B", "C", "D", "E" })
                await CreateHabit(name);

            var result = await _habitService.GetPagedAsync(new HabitListQueryDto { Page = 2, PageSize = 2 }, FirstUserId, CancellationToken.None);

            Assert.Equal(["C", "D"], result.Value!.Items.Select(habit => habit.Name));
            Assert.Equal(5, result.Value.TotalCount);
            Assert.Equal(3, result.Value.TotalPages);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(PageRequest.MaxPageSize + 1)]
        public async Task GetPagedAsync_ShouldReturnValidationError_WhenPageSizeIsOutOfRange(int pageSize)
        {
            var result = await _habitService.GetPagedAsync(new HabitListQueryDto { PageSize = pageSize }, FirstUserId, CancellationToken.None);

            Assert.Equal(PagingErrors.InvalidPageSize, result.Error);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnValidationError_WhenPageIsInvalid()
        {
            var result = await _habitService.GetPagedAsync(new HabitListQueryDto { Page = 0 }, FirstUserId, CancellationToken.None);

            Assert.Equal(PagingErrors.InvalidPage, result.Error);
        }
    }
}
