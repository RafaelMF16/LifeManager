using LifeManager.Application.RecurringTransactions.DTOs;
using LifeManager.Application.RecurringTransactions.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.RecurringTransactions.Mocks;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.RecurringTransactions;
using LifeManager.Domain.RecurringTransactions.Enums;
using LifeManager.Domain.RecurringTransactions.Errors;
using LifeManager.Domain.RecurringTransactions.Interfaces;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Transactions.Enums;
using LifeManager.Domain.Transactions.Errors;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.RecurringTransactions
{
    [Collection("ApplicationServices")]
    public class RecurringTransactionServiceTest : BaseTest
    {
        private static readonly UserId FirstUserId = new(1);
        private static readonly UserId SecondUserId = new(2);
        private static readonly DateOnly Today = new(2026, 10, 6);

        private const int SalaryCategoryId = 1;
        private const int RentCategoryId = 2;
        private const int OtherUserCategoryId = 3;

        private readonly RecurringTransactionService _recurringTransactionService;
        private readonly RecurringTransactionRepositoryMock _recurringTransactionRepository;
        private readonly FakeTimeProvider _timeProvider;

        public RecurringTransactionServiceTest()
        {
            _recurringTransactionService = ServiceProvider.GetRequiredService<RecurringTransactionService>();
            _recurringTransactionRepository = (RecurringTransactionRepositoryMock)ServiceProvider.GetRequiredService<IRecurringTransactionRepository>();
            _timeProvider = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();
            _timeProvider.SetToday(Today);

            RecurringTransactionSingleton.Instance.Clear();
            TransactionSingleton.Instance.Clear();
            MonthlySummarySingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();

            SeedCategory(SalaryCategoryId, FirstUserId, "Salário");
            SeedCategory(RentCategoryId, FirstUserId, "Aluguel");
            SeedCategory(OtherUserCategoryId, SecondUserId, "Outros");
        }

        [Fact]
        public async Task CreateAsync_ShouldAddRecurrence_AndPostNothing_WhenTheFirstDayIsAhead()
        {
            var result = await _recurringTransactionService.CreateAsync(Rent(day: 10), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                new RecurringTransactionResponseDto(1, MoneyFlowType.Expense, RentCategoryId, "Aluguel", 1500m, "Aluguel", 10, "2026-10", null,
                    RecurringTransactionStatus.Active, new DateOnly(2026, 10, 10)),
                result.Value);
            var stored = Assert.Single(RecurringTransactionSingleton.Instance);
            Assert.Equal(FirstUserId, stored.UserId);
            Assert.Empty(TransactionSingleton.Instance);
            Assert.Empty(MonthlySummarySingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldPostRightAway_WhenTheDayAlreadyPassedThisMonth()
        {
            var result = await _recurringTransactionService.CreateAsync(Salary(day: 5), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(new DateOnly(2026, 11, 5), result.Value.NextOccurrenceDate);
            var transaction = Assert.Single(TransactionSingleton.Instance);
            Assert.Equal(new DateOnly(2026, 10, 5), transaction.TransactionDate);
            Assert.Equal(result.Value.Id, transaction.RecurringTransactionId!.Value);
            Assert.Equal(8000m, Assert.Single(MonthlySummarySingleton.Instance).TotalIncome.Value);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnCategoryNotFound_WhenCategoryBelongsToOtherUser()
        {
            var result = await _recurringTransactionService.CreateAsync(Salary(categoryId: OtherUserCategoryId), FirstUserId, CancellationToken.None);

            Assert.Equal(CategoryErrors.NotFound, result.Error);
            Assert.Empty(RecurringTransactionSingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnDomainError_WhenValuesAreInvalid()
        {
            var startInPast = await _recurringTransactionService.CreateAsync(Salary(start: "2026-09"), FirstUserId, CancellationToken.None);
            var invalidAmount = await _recurringTransactionService.CreateAsync(Salary() with { Amount = 0m }, FirstUserId, CancellationToken.None);

            Assert.Equal(RecurringTransactionErrors.StartInPast, startInPast.Error);
            Assert.Equal(TransactionErrors.AmountNotPositive, invalidAmount.Error);
            Assert.Empty(RecurringTransactionSingleton.Instance);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNotFound_WhenRecurrenceBelongsToOtherUser()
        {
            var created = await Create(Rent(day: 10));

            var own = await _recurringTransactionService.GetByIdAsync(created.Id, FirstUserId, CancellationToken.None);
            var other = await _recurringTransactionService.GetByIdAsync(created.Id, SecondUserId, CancellationToken.None);

            Assert.Equal(created, own.Value);
            Assert.Equal(RecurringTransactionErrors.NotFound, other.Error);
        }

        [Fact]
        public async Task UpdateAsync_ShouldChangeTheNextOccurrences_AndKeepWhatWasPosted()
        {
            var created = await Create(Salary(day: 5));

            var result = await _recurringTransactionService.UpdateAsync(
                created.Id, Salary(day: 20) with { Amount = 9000m, Description = "Salário novo" }, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(9000m, result.Value.Amount);
            Assert.Equal(new DateOnly(2026, 11, 20), result.Value.NextOccurrenceDate);
            var transaction = Assert.Single(TransactionSingleton.Instance);
            Assert.Equal(8000m, transaction.Amount.Value);
            Assert.Equal("Salário", transaction.Description.Value);
        }

        [Fact]
        public async Task UpdateAsync_ShouldPostRightAway_WhenTheNewDayAlreadyPassed()
        {
            var created = await Create(Rent(day: 10));

            var result = await _recurringTransactionService.UpdateAsync(created.Id, Rent(day: 1), FirstUserId, CancellationToken.None);

            Assert.Equal(new DateOnly(2026, 11, 1), result.Value!.NextOccurrenceDate);
            Assert.Equal(new DateOnly(2026, 10, 1), Assert.Single(TransactionSingleton.Instance).TransactionDate);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnStartLocked_WhenTheRecurrenceHasStarted()
        {
            var created = await Create(Salary(day: 5));

            var result = await _recurringTransactionService.UpdateAsync(created.Id, Salary(day: 5, start: "2026-12"), FirstUserId, CancellationToken.None);

            Assert.Equal(RecurringTransactionErrors.StartLocked, result.Error);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNotFound_WhenRecurrenceBelongsToOtherUser()
        {
            var created = await Create(Rent(day: 10));

            var result = await _recurringTransactionService.UpdateAsync(created.Id, Rent(day: 12), SecondUserId, CancellationToken.None);

            Assert.Equal(RecurringTransactionErrors.NotFound, result.Error);
            Assert.Equal(10, Assert.Single(RecurringTransactionSingleton.Instance).DayOfMonth.Value);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnChangedConcurrently_WhenTheCursorMovedMeanwhile()
        {
            var created = await Create(Rent(day: 10));
            _recurringTransactionRepository.BeforeNextUpdate = () => RecurringTransactionSingleton.Instance.Single().AdvanceAfterPosting();

            var result = await _recurringTransactionService.UpdateAsync(created.Id, Rent(day: 12), FirstUserId, CancellationToken.None);

            Assert.Equal(RecurringTransactionErrors.ChangedConcurrently, result.Error);
            Assert.Equal(10, Assert.Single(RecurringTransactionSingleton.Instance).DayOfMonth.Value);
        }

        [Fact]
        public async Task PauseAsync_ShouldStopPosting()
        {
            var created = await Create(Rent(day: 10));

            var result = await _recurringTransactionService.PauseAsync(created.Id, FirstUserId, CancellationToken.None);
            _timeProvider.SetToday(new DateOnly(2026, 10, 15));
            await ServiceProvider.GetRequiredService<RecurringTransactionPostingService>()
                .PostDueOccurrencesAsync(new(created.Id), CancellationToken.None);

            Assert.Equal(RecurringTransactionStatus.Paused, result.Value!.Status);
            Assert.Empty(TransactionSingleton.Instance);
        }

        [Fact]
        public async Task PauseAsync_ShouldReturnAlreadyPaused_WhenPaused()
        {
            var created = await Create(Rent(day: 10));
            await _recurringTransactionService.PauseAsync(created.Id, FirstUserId, CancellationToken.None);

            var result = await _recurringTransactionService.PauseAsync(created.Id, FirstUserId, CancellationToken.None);

            Assert.Equal(RecurringTransactionErrors.AlreadyPaused, result.Error);
        }

        [Fact]
        public async Task ResumeAsync_ShouldSkipThePausedMonths_AndPostThisMonth()
        {
            var created = await Create(Rent(day: 10));
            await _recurringTransactionService.PauseAsync(created.Id, FirstUserId, CancellationToken.None);
            _timeProvider.SetToday(new DateOnly(2027, 1, 15));

            var result = await _recurringTransactionService.ResumeAsync(created.Id, FirstUserId, CancellationToken.None);

            Assert.Equal(RecurringTransactionStatus.Active, result.Value!.Status);
            Assert.Equal(new DateOnly(2027, 2, 10), result.Value.NextOccurrenceDate);
            Assert.Equal(new DateOnly(2027, 1, 10), Assert.Single(TransactionSingleton.Instance).TransactionDate);
        }

        [Fact]
        public async Task ResumeAsync_ShouldReturnNotPaused_WhenActive()
        {
            var created = await Create(Rent(day: 10));

            var result = await _recurringTransactionService.ResumeAsync(created.Id, FirstUserId, CancellationToken.None);

            Assert.Equal(RecurringTransactionErrors.NotPaused, result.Error);
        }

        [Fact]
        public async Task DeleteAsync_ShouldRemoveRecurrence_AndKeepItsTransactionsWithoutTheLink()
        {
            var created = await Create(Salary(day: 5));

            var result = await _recurringTransactionService.DeleteAsync(created.Id, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(RecurringTransactionSingleton.Instance);
            Assert.Null(Assert.Single(TransactionSingleton.Instance).RecurringTransactionId);
        }

        [Fact]
        public async Task DeleteAsync_ShouldReturnNotFound_WhenRecurrenceBelongsToOtherUser()
        {
            var created = await Create(Rent(day: 10));

            var result = await _recurringTransactionService.DeleteAsync(created.Id, SecondUserId, CancellationToken.None);

            Assert.Equal(RecurringTransactionErrors.NotFound, result.Error);
            Assert.Single(RecurringTransactionSingleton.Instance);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnOnlyTheUsersRecurrences_SortedByNextOccurrenceWithFinishedLast()
        {
            SeedRecurrence(1, "Academia", day: 20);
            SeedRecurrence(2, "Internet", day: 15);
            SeedRecurrence(3, "Curso", day: 1, end: "2026-09", start: "2026-09");
            SeedRecurrence(4, "Outro usuário", day: 1, userId: SecondUserId, categoryId: OtherUserCategoryId);

            var result = await _recurringTransactionService.GetPagedAsync(new RecurringTransactionListQueryDto(), FirstUserId, CancellationToken.None);

            Assert.Equal(["Internet", "Academia", "Curso"], result.Value!.Items.Select(item => item.Description).ToArray());
            Assert.Equal(3, result.Value.TotalCount);
            Assert.Equal(RecurringTransactionStatus.Finished, result.Value.Items[2].Status);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldSortFinishedFirst_WhenDescending()
        {
            SeedRecurrence(1, "Academia", day: 20);
            SeedRecurrence(2, "Curso", day: 1, end: "2026-09", start: "2026-09");

            var result = await _recurringTransactionService.GetPagedAsync(
                new RecurringTransactionListQueryDto { SortDirection = SortDirection.Desc }, FirstUserId, CancellationToken.None);

            Assert.Equal(["Curso", "Academia"], result.Value!.Items.Select(item => item.Description).ToArray());
        }

        [Fact]
        public async Task GetPagedAsync_ShouldSearchIgnoringAccentsAndCase()
        {
            SeedRecurrence(1, "Salário", day: 5);
            SeedRecurrence(2, "Aluguel", day: 10);

            var result = await _recurringTransactionService.GetPagedAsync(
                new RecurringTransactionListQueryDto { Search = "  SALARIO " }, FirstUserId, CancellationToken.None);

            Assert.Equal("Salário", Assert.Single(result.Value!.Items).Description);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldFilterByTypeAndStatus()
        {
            SeedRecurrence(1, "Salário", day: 5, type: MoneyFlowType.Income);
            SeedRecurrence(2, "Aluguel", day: 10, isActive: false);
            SeedRecurrence(3, "Mercado", day: 12);

            var income = await _recurringTransactionService.GetPagedAsync(
                new RecurringTransactionListQueryDto { Type = TransactionTypeFilter.Income }, FirstUserId, CancellationToken.None);
            var paused = await _recurringTransactionService.GetPagedAsync(
                new RecurringTransactionListQueryDto { Status = RecurringTransactionStatusFilter.Paused }, FirstUserId, CancellationToken.None);
            var active = await _recurringTransactionService.GetPagedAsync(
                new RecurringTransactionListQueryDto { Status = RecurringTransactionStatusFilter.Active }, FirstUserId, CancellationToken.None);

            Assert.Equal("Salário", Assert.Single(income.Value!.Items).Description);
            Assert.Equal("Aluguel", Assert.Single(paused.Value!.Items).Description);
            Assert.Equal(["Salário", "Mercado"], active.Value!.Items.Select(item => item.Description).ToArray());
        }

        [Fact]
        public async Task GetPagedAsync_ShouldSortByAmountAndDay()
        {
            SeedRecurrence(1, "Aluguel", day: 10, amount: 1500m);
            SeedRecurrence(2, "Internet", day: 25, amount: 120m);
            SeedRecurrence(3, "Academia", day: 1, amount: 99.90m);

            var byAmount = await _recurringTransactionService.GetPagedAsync(
                new RecurringTransactionListQueryDto { SortBy = RecurringTransactionSortBy.Amount, SortDirection = SortDirection.Desc }, FirstUserId, CancellationToken.None);
            var byDay = await _recurringTransactionService.GetPagedAsync(
                new RecurringTransactionListQueryDto { SortBy = RecurringTransactionSortBy.Day }, FirstUserId, CancellationToken.None);

            Assert.Equal(["Aluguel", "Internet", "Academia"], byAmount.Value!.Items.Select(item => item.Description).ToArray());
            Assert.Equal(["Academia", "Aluguel", "Internet"], byDay.Value!.Items.Select(item => item.Description).ToArray());
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnTheSecondPage_AndAnEmptyPageBeyondTheLast()
        {
            for (var id = 1; id <= 3; id++)
                SeedRecurrence(id, $"Conta {id}", day: 10 + id);

            var second = await _recurringTransactionService.GetPagedAsync(
                new RecurringTransactionListQueryDto { Page = 2, PageSize = 2 }, FirstUserId, CancellationToken.None);
            var beyond = await _recurringTransactionService.GetPagedAsync(
                new RecurringTransactionListQueryDto { Page = 5, PageSize = 2 }, FirstUserId, CancellationToken.None);

            Assert.Equal("Conta 3", Assert.Single(second.Value!.Items).Description);
            Assert.Equal(3, second.Value.TotalCount);
            Assert.Equal(2, second.Value.TotalPages);
            Assert.Empty(beyond.Value!.Items);
            Assert.Equal(3, beyond.Value.TotalCount);
        }

        [Theory]
        [InlineData(0, 20)]
        [InlineData(1, 0)]
        [InlineData(1, PageRequest.MaxPageSize + 1)]
        public async Task GetPagedAsync_ShouldReturnPagingError_WhenPageOrPageSizeIsInvalid(int page, int pageSize)
        {
            var result = await _recurringTransactionService.GetPagedAsync(
                new RecurringTransactionListQueryDto { Page = page, PageSize = pageSize }, FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Error, new[] { PagingErrors.InvalidPage, PagingErrors.InvalidPageSize });
        }

        private async Task<RecurringTransactionResponseDto> Create(RecurringTransactionDto dto)
            => (await _recurringTransactionService.CreateAsync(dto, FirstUserId, CancellationToken.None)).Value!;

        private static RecurringTransactionDto Salary(int day = 5, string start = "2026-10", int categoryId = SalaryCategoryId)
            => new(MoneyFlowType.Income, categoryId, 8000m, "Salário", day, start, null);

        private static RecurringTransactionDto Rent(int day)
            => new(MoneyFlowType.Expense, RentCategoryId, 1500m, "Aluguel", day, "2026-10", null);

        private static void SeedRecurrence(
            int id,
            string description,
            int day,
            string start = "2026-10",
            string? end = null,
            bool isActive = true,
            UserId? userId = null,
            MoneyFlowType type = MoneyFlowType.Expense,
            int categoryId = RentCategoryId,
            decimal amount = 100m)
        {
            YearMonth.TryParse(start, out var startMonth);
            YearMonth? endMonth = null;
            if (end is not null && YearMonth.TryParse(end, out var parsedEnd))
                endMonth = parsedEnd;

            // A finished recurrence's cursor sits right after its end month.
            var nextMonth = endMonth is null ? startMonth : endMonth.AddMonths(1);

            RecurringTransactionSingleton.Instance.Add(RecurringTransaction.FromPersistence(
                id, (userId ?? FirstUserId).Value, type, categoryId, amount, description, day, startMonth, endMonth, isActive, nextMonth));
        }

        private static void SeedCategory(int id, UserId userId, string name)
        {
            var category = Category.Create(userId.Value, name).Value!;
            category.AssignId(id);
            CategorySingleton.Instance.Add(category);
        }
    }
}
