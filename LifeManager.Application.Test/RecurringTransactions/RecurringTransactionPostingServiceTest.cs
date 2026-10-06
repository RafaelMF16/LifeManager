using LifeManager.Application.RecurringTransactions.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.MonthlySummaries.Mocks;
using LifeManager.Application.Test.RecurringTransactions.Mocks;
using LifeManager.Domain.Categories;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.Interfaces;
using LifeManager.Domain.RecurringTransactions;
using LifeManager.Domain.RecurringTransactions.Interfaces;
using LifeManager.Domain.RecurringTransactions.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.RecurringTransactions
{
    [Collection("ApplicationServices")]
    public class RecurringTransactionPostingServiceTest : BaseTest
    {
        private static readonly UserId FirstUserId = new(1);
        private static readonly UserId SecondUserId = new(2);
        private static readonly DateOnly Today = new(2026, 10, 6);

        private const int SalaryCategoryId = 1;
        private const int RentCategoryId = 2;
        private const int OtherUserCategoryId = 3;

        private readonly RecurringTransactionPostingService _postingService;
        private readonly RecurringTransactionRepositoryMock _recurringTransactionRepository;
        private readonly MonthlySummaryRepositoryMock _monthlySummaryRepository;
        private readonly FakeTimeProvider _timeProvider;

        public RecurringTransactionPostingServiceTest()
        {
            _postingService = ServiceProvider.GetRequiredService<RecurringTransactionPostingService>();
            _recurringTransactionRepository = (RecurringTransactionRepositoryMock)ServiceProvider.GetRequiredService<IRecurringTransactionRepository>();
            _monthlySummaryRepository = (MonthlySummaryRepositoryMock)ServiceProvider.GetRequiredService<IMonthlySummaryRepository>();
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
        public async Task PostDueOccurrencesAsync_ShouldPostIntoTheMonthAndUpdateItsTotals_WhenTheDayHasCome()
        {
            MonthlySummarySingleton.Instance.Add(MonthlySummary.FromPersistence(1, FirstUserId.Value, 10, 2026, 0, 0, 0));
            SeedRecurrence(1, day: 5, nextMonth: "2026-10");

            var result = await _postingService.PostDueOccurrencesAsync(new RecurringTransactionId(1), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value);
            var transaction = Assert.Single(TransactionSingleton.Instance);
            Assert.Equal(1, transaction.MonthlySummaryId.Value);
            Assert.Equal(new DateOnly(2026, 10, 5), transaction.TransactionDate);
            Assert.Equal(MoneyFlowType.Income, transaction.Type);
            Assert.Equal(8000m, transaction.Amount.Value);
            Assert.Equal("Salário", transaction.Description.Value);
            Assert.Equal(1, transaction.RecurringTransactionId!.Value);
            Assert.Equal(8000m, MonthlySummarySingleton.Instance.Single().TotalIncome.Value);
            Assert.Equal(Month("2026-11"), StoredRecurrence(1).NextMonth);
            Assert.Equal(0, _monthlySummaryRepository.AddCallCount);
        }

        [Fact]
        public async Task PostDueOccurrencesAsync_ShouldOpenTheMonth_WhenTheUserHasNotOpenedIt()
        {
            SeedRecurrence(1, day: 5, nextMonth: "2026-10", type: MoneyFlowType.Expense, categoryId: RentCategoryId, amount: 1500m);

            var result = await _postingService.PostDueOccurrencesAsync(new RecurringTransactionId(1), CancellationToken.None);

            Assert.Equal(1, result.Value);
            var monthlySummary = Assert.Single(MonthlySummarySingleton.Instance);
            Assert.Equal(FirstUserId, monthlySummary.UserId);
            Assert.Equal(10, monthlySummary.Month.Value);
            Assert.Equal(2026, monthlySummary.Year.Value);
            Assert.Equal(1500m, monthlySummary.TotalExpense.Value);
            Assert.Equal(-1500m, monthlySummary.BalanceAmount);
            Assert.Equal(monthlySummary.Id, Assert.Single(TransactionSingleton.Instance).MonthlySummaryId);
        }

        [Fact]
        public async Task PostDueOccurrencesAsync_ShouldCatchUpEveryMissedMonth_InOrder()
        {
            SeedRecurrence(1, day: 5, start: "2026-07", nextMonth: "2026-07");

            var result = await _postingService.PostDueOccurrencesAsync(new RecurringTransactionId(1), CancellationToken.None);

            Assert.Equal(4, result.Value);
            Assert.Equal(
                [new DateOnly(2026, 7, 5), new DateOnly(2026, 8, 5), new DateOnly(2026, 9, 5), new DateOnly(2026, 10, 5)],
                TransactionSingleton.Instance.Select(transaction => transaction.TransactionDate).Order().ToArray());
            Assert.Equal(4, MonthlySummarySingleton.Instance.Count);
            Assert.All(MonthlySummarySingleton.Instance, monthlySummary => Assert.Equal(8000m, monthlySummary.TotalIncome.Value));
            Assert.Equal(new DateOnly(2026, 11, 5), StoredRecurrence(1).NextOccurrenceDate);
        }

        [Fact]
        public async Task PostDueOccurrencesAsync_ShouldPostNothing_WhenRunAgain()
        {
            SeedRecurrence(1, day: 5, nextMonth: "2026-10");
            await _postingService.PostDueOccurrencesAsync(new RecurringTransactionId(1), CancellationToken.None);

            var result = await _postingService.PostDueOccurrencesAsync(new RecurringTransactionId(1), CancellationToken.None);

            Assert.Equal(0, result.Value);
            Assert.Single(TransactionSingleton.Instance);
        }

        [Fact]
        public async Task PostDueOccurrencesAsync_ShouldPostNothing_BeforeTheDay()
        {
            SeedRecurrence(1, day: 10, nextMonth: "2026-10");

            var result = await _postingService.PostDueOccurrencesAsync(new RecurringTransactionId(1), CancellationToken.None);

            Assert.Equal(0, result.Value);
            Assert.Empty(TransactionSingleton.Instance);
            Assert.Empty(MonthlySummarySingleton.Instance);
        }

        [Fact]
        public async Task PostDueOccurrencesAsync_ShouldPostNothing_WhenPaused()
        {
            SeedRecurrence(1, day: 5, nextMonth: "2026-10", isActive: false);

            var result = await _postingService.PostDueOccurrencesAsync(new RecurringTransactionId(1), CancellationToken.None);

            Assert.Equal(0, result.Value);
            Assert.Empty(TransactionSingleton.Instance);
        }

        [Fact]
        public async Task PostDueOccurrencesAsync_ShouldStopAtTheEndMonth()
        {
            SeedRecurrence(1, day: 5, start: "2026-08", end: "2026-09", nextMonth: "2026-08");

            var result = await _postingService.PostDueOccurrencesAsync(new RecurringTransactionId(1), CancellationToken.None);

            Assert.Equal(2, result.Value);
            Assert.Null(StoredRecurrence(1).NextOccurrenceDate);
        }

        [Fact]
        public async Task PostDueOccurrencesAsync_ShouldPostIntoLastYear_WhenCatchingUpAfterTheYearTurns()
        {
            _timeProvider.SetToday(new DateOnly(2027, 1, 2));
            SeedRecurrence(1, day: 31, start: "2026-12", nextMonth: "2026-12");

            var result = await _postingService.PostDueOccurrencesAsync(new RecurringTransactionId(1), CancellationToken.None);

            Assert.Equal(1, result.Value);
            Assert.Equal(new DateOnly(2026, 12, 31), Assert.Single(TransactionSingleton.Instance).TransactionDate);
            var monthlySummary = Assert.Single(MonthlySummarySingleton.Instance);
            Assert.Equal(2026, monthlySummary.Year.Value);
            Assert.Equal(12, monthlySummary.Month.Value);
            Assert.Equal(new DateOnly(2027, 1, 31), StoredRecurrence(1).NextOccurrenceDate);
        }

        [Fact]
        public async Task PostDueOccurrencesAsync_ShouldStopQuietly_WhenAnotherRunMovedTheCursor()
        {
            SeedRecurrence(1, day: 5, nextMonth: "2026-10");
            _recurringTransactionRepository.BeforeNextPost = () =>
            {
                var stored = StoredRecurrence(1);
                stored.AdvanceAfterPosting();
            };

            var result = await _postingService.PostDueOccurrencesAsync(new RecurringTransactionId(1), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.Value);
            Assert.Empty(TransactionSingleton.Instance);
        }

        [Fact]
        public async Task PostDueOccurrencesAsync_ShouldPostNothing_WhenTheRecurrenceIsGone()
        {
            var result = await _postingService.PostDueOccurrencesAsync(new RecurringTransactionId(99), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.Value);
        }

        [Fact]
        public async Task PostDueOccurrencesAsync_ShouldPostIntoTheOwnersMonth()
        {
            MonthlySummarySingleton.Instance.Add(MonthlySummary.FromPersistence(1, FirstUserId.Value, 10, 2026, 0, 0, 0));
            SeedRecurrence(1, day: 5, nextMonth: "2026-10", userId: SecondUserId, categoryId: OtherUserCategoryId);

            await _postingService.PostDueOccurrencesAsync(new RecurringTransactionId(1), CancellationToken.None);

            var created = MonthlySummarySingleton.Instance.Single(monthlySummary => monthlySummary.UserId == SecondUserId);
            Assert.Equal(created.Id, Assert.Single(TransactionSingleton.Instance).MonthlySummaryId);
            Assert.Equal(0m, MonthlySummarySingleton.Instance.Single(monthlySummary => monthlySummary.UserId == FirstUserId).TotalIncome.Value);
        }

        [Fact]
        public async Task GetDueIdsAsync_ShouldReturnEveryUsersDueActiveRecurrences_OldestFirst()
        {
            SeedRecurrence(1, day: 5, nextMonth: "2026-10");
            SeedRecurrence(2, day: 6, nextMonth: "2026-10", userId: SecondUserId, categoryId: OtherUserCategoryId);
            SeedRecurrence(3, day: 20, start: "2026-09", nextMonth: "2026-09");
            SeedRecurrence(4, day: 7, nextMonth: "2026-10");
            SeedRecurrence(5, day: 1, nextMonth: "2026-10", isActive: false);
            SeedRecurrence(6, day: 1, start: "2026-08", end: "2026-09", nextMonth: "2026-10");

            var ids = await _postingService.GetDueIdsAsync(CancellationToken.None);

            Assert.Equal([3, 1, 2], ids.Select(id => id.Value).ToArray());
        }

        private static void SeedRecurrence(
            int id,
            int day,
            string nextMonth,
            string start = "2026-10",
            string? end = null,
            bool isActive = true,
            UserId? userId = null,
            MoneyFlowType type = MoneyFlowType.Income,
            int categoryId = SalaryCategoryId,
            decimal amount = 8000m)
        {
            RecurringTransactionSingleton.Instance.Add(RecurringTransaction.FromPersistence(
                id,
                (userId ?? FirstUserId).Value,
                type,
                categoryId,
                amount,
                type == MoneyFlowType.Income ? "Salário" : "Aluguel",
                day,
                Month(start),
                end is null ? null : Month(end),
                isActive,
                Month(nextMonth)));
        }

        private static RecurringTransaction StoredRecurrence(int id)
            => RecurringTransactionSingleton.Instance.Single(recurrence => recurrence.Id!.Value == id);

        private static YearMonth Month(string value)
        {
            YearMonth.TryParse(value, out var month);
            return month;
        }

        private static void SeedCategory(int id, UserId userId, string name)
        {
            var category = Category.Create(userId.Value, name).Value!;
            category.AssignId(id);
            CategorySingleton.Instance.Add(category);
        }
    }
}
