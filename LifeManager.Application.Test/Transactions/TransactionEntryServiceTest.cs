using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.Habits.Mocks;
using LifeManager.Application.Transactions.DTOs;
using LifeManager.Application.Transactions.Services;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.Errors;
using LifeManager.Domain.SavingsGoals;
using LifeManager.Domain.SavingsGoals.Errors;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Transactions.Errors;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Transactions
{
    /// <summary><c>TransactionService.CreateQuickAsync</c>: a transaction entered without picking its month first.</summary>
    [Collection("ApplicationServices")]
    public class TransactionEntryServiceTest : BaseTest
    {
        private static readonly UserId FirstUserId = new(1);
        private static readonly UserId SecondUserId = new(2);
        private const int CurrentYear = 2026;
        private static readonly DateOnly Today = new(CurrentYear, 11, 10);

        private const int MarketCategoryId = 1;
        private const int SalaryCategoryId = 2;
        private const int OtherUserCategoryId = 3;

        private readonly TransactionService _transactionService;
        private readonly FakeTimeProvider _clock;

        public TransactionEntryServiceTest()
        {
            _transactionService = ServiceProvider.GetRequiredService<TransactionService>();
            _clock = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();
            _clock.SetToday(Today);

            TransactionSingleton.Instance.Clear();
            MonthlySummarySingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();
            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            PlayerProfileSingleton.Instance.Clear();
            GameLedgerEntrySingleton.Instance.Clear();
            SavingsGoalSingleton.Instance.Clear();
            HabitCheckInRepositoryMock.FailNextRecord = null;

            SeedCategory(MarketCategoryId, FirstUserId, "Mercado");
            SeedCategory(SalaryCategoryId, FirstUserId, "Salário");
            SeedCategory(OtherUserCategoryId, SecondUserId, "Outros");
        }

        [Fact]
        public async Task CreateQuickAsync_ShouldPostIntoTheExistingMonth_EvenThreeYearsAgo()
        {
            MonthlySummarySingleton.Instance.Add(MonthlySummary.FromPersistence(7, FirstUserId.Value, 4, CurrentYear - 3, 0, 0, 0));

            var result = await CreateQuick(Expense("Supermercado", 80m, new DateOnly(CurrentYear - 3, 4, 12)));

            Assert.True(result.IsSuccess);
            Assert.Equal(7, result.Value!.MonthlySummaryId);
            Assert.False(result.Value.MonthCreated);
            Assert.Equal(new DateOnly(CurrentYear - 3, 4, 12), result.Value.Date);
            Assert.Single(MonthlySummarySingleton.Instance);
            Assert.Equal(80m, Month(CurrentYear - 3, 4).TotalExpense.Value);
        }

        [Fact]
        public async Task CreateQuickAsync_ShouldOpenTheMissingMonthAndUpdateItsTotals_WhenItIsInTheCurrentYear()
        {
            var first = await CreateQuick(Expense("  Supermercado  ", 120.50m, Today));
            var second = await CreateQuick(Income("Salário", 1000m, Today.AddDays(-5)));

            Assert.True(first.IsSuccess);
            Assert.True(first.Value!.MonthCreated);
            Assert.Equal(
                new QuickTransactionResponseDto(
                    1, MoneyFlowType.Expense, MarketCategoryId, "Mercado", 120.50m, "Supermercado", Today, null,
                    first.Value.MonthlySummaryId, true, first.Value.AutoCheckIns),
                first.Value);

            Assert.True(second.IsSuccess);
            Assert.False(second.Value!.MonthCreated);
            Assert.Equal(first.Value.MonthlySummaryId, second.Value.MonthlySummaryId);

            var month = Month(CurrentYear, 11);
            Assert.Equal(FirstUserId, month.UserId);
            Assert.Equal(1000m, month.TotalIncome.Value);
            Assert.Equal(120.50m, month.TotalExpense.Value);
            Assert.Equal(879.50m, month.BalanceAmount);
        }

        [Fact]
        public async Task CreateQuickAsync_ShouldOpenLastDecember_WhenItIsJanuary()
        {
            _clock.SetToday(new DateOnly(CurrentYear, 1, 3));

            var result = await CreateQuick(Expense("Ceia", 300m, new DateOnly(CurrentYear - 1, 12, 15)));

            Assert.True(result.IsSuccess);
            Assert.True(result.Value!.MonthCreated);
            Assert.Equal(300m, Month(CurrentYear - 1, 12).TotalExpense.Value);
        }

        [Theory]
        [InlineData(-2)]
        [InlineData(1)]
        public async Task CreateQuickAsync_ShouldReturnYearNotCurrent_WhenTheMissingMonthIsNeitherThisYearNorThePrevious(int offsetFromCurrentYear)
        {
            var result = await CreateQuick(Expense("Supermercado", 10m, new DateOnly(CurrentYear + offsetFromCurrentYear, 5, 2)));

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.YearNotCurrent, result.Error);
            AssertNothingStored();
        }

        [Fact]
        public async Task CreateQuickAsync_ShouldReturnYearNotCurrent_WhenTheDateIsBefore2000()
        {
            var result = await CreateQuick(Expense("Supermercado", 10m, new DateOnly(1999, 12, 31)));

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.YearNotCurrent, result.Error);
            AssertNothingStored();
        }

        [Fact]
        public async Task CreateQuickAsync_ShouldNotOpenTheMonth_WhenTheCategoryBelongsToAnotherUser()
        {
            var dto = new TransactionDto(MoneyFlowType.Expense, OtherUserCategoryId, 10m, "Supermercado", Today);

            var result = await CreateQuick(dto);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NotFound, result.Error);
            AssertNothingStored();
        }

        [Fact]
        public async Task CreateQuickAsync_ShouldNotOpenTheMonth_WhenTheTransactionIsInvalid()
        {
            var result = await CreateQuick(Expense("   ", 10m, Today));

            Assert.False(result.IsSuccess);
            Assert.Equal(TransactionErrors.DescriptionIsNullOrWhiteSpace, result.Error);
            AssertNothingStored();
        }

        [Fact]
        public async Task CreateQuickAsync_ShouldOpenTheUsersOwnMonth_WhenAnotherUserHasThatPeriod()
        {
            MonthlySummarySingleton.Instance.Add(MonthlySummary.FromPersistence(5, SecondUserId.Value, 11, CurrentYear, 0, 0, 0));

            var result = await CreateQuick(Expense("Supermercado", 10m, Today));

            Assert.True(result.IsSuccess);
            Assert.True(result.Value!.MonthCreated);
            Assert.NotEqual(5, result.Value.MonthlySummaryId);
            Assert.Equal(0m, MonthlySummarySingleton.Instance.Single(month => month.Id!.Value == 5).TotalExpense.Value);
        }

        [Fact]
        public async Task CreateQuickAsync_ShouldCheckInTheHabitsOfTheTrigger()
        {
            HabitSingleton.Instance.Add(Habit.FromPersistence(
                1, FirstUserId.Value, "Log my spending", null, null, HabitKind.Positive, HabitDifficulty.Easy,
                HabitFrequencyType.Daily, HabitWeekDays.None, null, Today.AddDays(-10), DateTimeOffset.UnixEpoch,
                null, currentStreak: 0, longestStreak: 0, evaluatedUntil: Today.AddDays(-2),
                autoTrigger: HabitAutoTrigger.TransactionCreated));

            var result = await CreateQuick(Expense("Supermercado", 50m, Today));

            Assert.True(result.IsSuccess);
            Assert.Equal("Log my spending", Assert.Single(result.Value!.AutoCheckIns!).Name);
            Assert.Equal(Today, Assert.Single(HabitCheckInSingleton.Instance).Date);
        }

        private Task<Domain.Shared.Results.Result<QuickTransactionResponseDto>> CreateQuick(TransactionDto dto)
            => _transactionService.CreateQuickAsync(dto, FirstUserId, CancellationToken.None);

        private static TransactionDto Expense(string description, decimal amount, DateOnly date)
            => new(MoneyFlowType.Expense, MarketCategoryId, amount, description, date);

        private static TransactionDto Income(string description, decimal amount, DateOnly date)
            => new(MoneyFlowType.Income, SalaryCategoryId, amount, description, date);

        private static MonthlySummary Month(int year, int month)
            => MonthlySummarySingleton.Instance.Single(stored =>
                stored.UserId == FirstUserId && stored.Year.Value == year && stored.Month.Value == month);

        private static void AssertNothingStored()
        {
            Assert.Empty(MonthlySummarySingleton.Instance);
            Assert.Empty(TransactionSingleton.Instance);
        }

        private static void SeedCategory(int id, UserId userId, string name)
        {
            var category = Category.Create(userId.Value, name).Value!;
            category.AssignId(id);
            CategorySingleton.Instance.Add(category);
        }

        [Fact]
        public async Task CreateQuickAsync_ShouldNotOpenTheMonth_WhenTheGoalIsArchived()
        {
            SavingsGoalSingleton.Instance.Add(SavingsGoal.FromPersistence(
                1, FirstUserId.Value, "Viagem", 1000m, null, 0m, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, completedAt: null));
            var dto = new TransactionDto(MoneyFlowType.Investment, MarketCategoryId, 100m, "Aporte", Today, SavingsGoalId: 1);

            var result = await CreateQuick(dto);

            Assert.Equal(SavingsGoalErrors.Archived, result.Error);
            AssertNothingStored();
        }

        [Fact]
        public async Task CreateQuickAsync_ShouldReturnTheGoal_WhenTheInvestmentIsLinked()
        {
            SavingsGoalSingleton.Instance.Add(SavingsGoal.FromPersistence(
                1, FirstUserId.Value, "Viagem", 1000m, null, 0m, null, DateTimeOffset.UnixEpoch, null, completedAt: null));

            var result = await CreateQuick(new TransactionDto(MoneyFlowType.Investment, MarketCategoryId, 100m, "Aporte", Today, SavingsGoalId: 1));

            Assert.Equal(1, result.Value!.SavingsGoalId);
        }
    }
}
