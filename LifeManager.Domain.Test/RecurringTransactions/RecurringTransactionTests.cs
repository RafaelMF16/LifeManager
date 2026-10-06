using LifeManager.Domain.RecurringTransactions;
using LifeManager.Domain.RecurringTransactions.Enums;
using LifeManager.Domain.RecurringTransactions.Errors;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Transactions.Errors;

namespace LifeManager.Domain.Test.RecurringTransactions
{
    public class RecurringTransactionTests
    {
        private static readonly DateOnly Today = new(2026, 10, 6);

        private static RecurringTransaction Salary(int day = 5, string start = "2026-10", string? end = null)
            => RecurringTransaction.Create(1, MoneyFlowType.Income, 2, 8000m, "Salário", day, start, end, Today).Value!;

        private static YearMonth Month(string value)
        {
            YearMonth.TryParse(value, out var month);
            return month;
        }

        [Fact]
        public void Create_ShouldReturnActiveRecurrence_WhenTheValuesAreValid()
        {
            var result = RecurringTransaction.Create(1, MoneyFlowType.Expense, 3, 1500.50m, "  Aluguel  ", 10, "2026-11", "2027-10", Today);

            Assert.True(result.IsSuccess);
            var recurrence = result.Value;
            Assert.Null(recurrence.Id);
            Assert.Equal(1, recurrence.UserId.Value);
            Assert.Equal(MoneyFlowType.Expense, recurrence.Type);
            Assert.Equal(3, recurrence.CategoryId.Value);
            Assert.Equal(1500.50m, recurrence.Amount.Value);
            Assert.Equal("Aluguel", recurrence.Description.Value);
            Assert.Equal("aluguel", recurrence.NormalizedDescription);
            Assert.Equal(10, recurrence.DayOfMonth.Value);
            Assert.Equal(Month("2026-11"), recurrence.StartMonth);
            Assert.Equal(Month("2027-10"), recurrence.EndMonth);
            Assert.Equal(Month("2026-11"), recurrence.NextMonth);
            Assert.Equal(new DateOnly(2026, 11, 10), recurrence.NextOccurrenceDate);
            Assert.Equal(RecurringTransactionStatus.Active, recurrence.Status);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Create_ShouldNeverEnd_WhenEndMonthIsEmpty(string? endMonth)
        {
            var recurrence = Salary(end: endMonth);

            Assert.Null(recurrence.EndMonth);
        }

        [Fact]
        public void Create_ShouldAllowCurrentMonth_WhenItsDayHasPassed()
        {
            var recurrence = Salary(day: 5, start: "2026-10");

            Assert.Equal(new DateOnly(2026, 10, 5), recurrence.NextOccurrenceDate);
            Assert.True(recurrence.IsDue(Today));
        }

        [Fact]
        public void Create_ShouldReturnStartInPast_WhenStartIsBeforeCurrentMonth()
        {
            var result = RecurringTransaction.Create(1, MoneyFlowType.Income, 2, 8000m, "Salário", 5, "2026-09", null, Today);

            Assert.False(result.IsSuccess);
            Assert.Equal(RecurringTransactionErrors.StartInPast, result.Error);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(32)]
        [InlineData(-1)]
        public void Create_ShouldReturnInvalidDay_WhenDayIsOutOfRange(int day)
        {
            var result = RecurringTransaction.Create(1, MoneyFlowType.Income, 2, 8000m, "Salário", day, "2026-10", null, Today);

            Assert.False(result.IsSuccess);
            Assert.Equal(RecurringTransactionErrors.InvalidDay, result.Error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("2026-13")]
        [InlineData("10/2026")]
        public void Create_ShouldReturnInvalidStartMonth_WhenStartIsNotYearMonth(string? startMonth)
        {
            var result = RecurringTransaction.Create(1, MoneyFlowType.Income, 2, 8000m, "Salário", 5, startMonth, null, Today);

            Assert.False(result.IsSuccess);
            Assert.Equal(RecurringTransactionErrors.InvalidStartMonth, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnInvalidEndMonth_WhenEndIsNotYearMonth()
        {
            var result = RecurringTransaction.Create(1, MoneyFlowType.Income, 2, 8000m, "Salário", 5, "2026-10", "2026-1", Today);

            Assert.False(result.IsSuccess);
            Assert.Equal(RecurringTransactionErrors.InvalidEndMonth, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnEndBeforeStart_WhenEndIsBeforeStart()
        {
            var result = RecurringTransaction.Create(1, MoneyFlowType.Income, 2, 8000m, "Salário", 5, "2026-11", "2026-10", Today);

            Assert.False(result.IsSuccess);
            Assert.Equal(RecurringTransactionErrors.EndBeforeStart, result.Error);
        }

        [Fact]
        public void Create_ShouldAllowSingleMonth_WhenEndEqualsStart()
        {
            var recurrence = Salary(start: "2026-11", end: "2026-11");

            recurrence.AdvanceAfterPosting();

            Assert.Null(recurrence.NextOccurrenceDate);
            Assert.Equal(RecurringTransactionStatus.Finished, recurrence.Status);
        }

        [Fact]
        public void Create_ShouldReuseTransactionRules_ForTypeAmountAndDescription()
        {
            Assert.Equal(TransactionErrors.InvalidType, RecurringTransaction.Create(1, (MoneyFlowType)99, 2, 10m, "x", 5, "2026-10", null, Today).Error);
            Assert.Equal(TransactionErrors.AmountNotPositive, RecurringTransaction.Create(1, MoneyFlowType.Income, 2, 0m, "x", 5, "2026-10", null, Today).Error);
            Assert.Equal(TransactionErrors.AmountTooManyDecimals, RecurringTransaction.Create(1, MoneyFlowType.Income, 2, 1.001m, "x", 5, "2026-10", null, Today).Error);
            Assert.Equal(TransactionErrors.DescriptionIsNullOrWhiteSpace, RecurringTransaction.Create(1, MoneyFlowType.Income, 2, 10m, "  ", 5, "2026-10", null, Today).Error);
            Assert.Equal(TransactionErrors.DescriptionTooLong, RecurringTransaction.Create(1, MoneyFlowType.Income, 2, 10m, new string('a', 81), 5, "2026-10", null, Today).Error);
        }

        [Theory]
        [InlineData("2027-02", 28)]
        [InlineData("2028-02", 29)]
        [InlineData("2026-11", 30)]
        [InlineData("2026-12", 31)]
        public void NextOccurrenceDate_ShouldFallOnLastDay_WhenMonthIsShorterThanTheDay(string month, int expectedDay)
        {
            var recurrence = Salary(day: 31, start: month);

            Assert.Equal(expectedDay, recurrence.NextOccurrenceDate!.Value.Day);
        }

        [Fact]
        public void AdvanceAfterPosting_ShouldMoveToNextMonth_AndKeepTheDay()
        {
            var recurrence = Salary(day: 31, start: "2027-01");

            recurrence.AdvanceAfterPosting();
            Assert.Equal(new DateOnly(2027, 2, 28), recurrence.NextOccurrenceDate);

            recurrence.AdvanceAfterPosting();
            Assert.Equal(new DateOnly(2027, 3, 31), recurrence.NextOccurrenceDate);
            Assert.Equal(Month("2027-03"), recurrence.NextMonth);
        }

        [Fact]
        public void AdvanceAfterPosting_ShouldCrossTheYear()
        {
            var recurrence = Salary(start: "2026-12");

            recurrence.AdvanceAfterPosting();

            Assert.Equal(new DateOnly(2027, 1, 5), recurrence.NextOccurrenceDate);
        }

        [Fact]
        public void AdvanceAfterPosting_ShouldFinish_AfterTheEndMonth()
        {
            var recurrence = Salary(start: "2026-10", end: "2026-11");

            recurrence.AdvanceAfterPosting();
            Assert.Equal(RecurringTransactionStatus.Active, recurrence.Status);

            recurrence.AdvanceAfterPosting();
            Assert.Null(recurrence.NextOccurrenceDate);
            Assert.Equal(RecurringTransactionStatus.Finished, recurrence.Status);
            Assert.False(recurrence.IsDue(new DateOnly(2030, 1, 1)));
        }

        [Fact]
        public void IsDue_ShouldBeTrueFromTheOccurrenceDay()
        {
            var recurrence = Salary(day: 10, start: "2026-10");

            Assert.False(recurrence.IsDue(new DateOnly(2026, 10, 9)));
            Assert.True(recurrence.IsDue(new DateOnly(2026, 10, 10)));
            Assert.True(recurrence.IsDue(new DateOnly(2026, 12, 1)));
        }

        [Fact]
        public void Pause_ShouldStopItFromBeingDue()
        {
            var recurrence = Salary();

            var result = recurrence.Pause();

            Assert.True(result.IsSuccess);
            Assert.False(recurrence.IsActive);
            Assert.Equal(RecurringTransactionStatus.Paused, recurrence.Status);
            Assert.False(recurrence.IsDue(Today));
        }

        [Fact]
        public void Pause_ShouldReturnAlreadyPaused_WhenPaused()
        {
            var recurrence = Salary();
            recurrence.Pause();

            Assert.Equal(RecurringTransactionErrors.AlreadyPaused, recurrence.Pause().Error);
        }

        [Fact]
        public void Resume_ShouldSkipThePausedMonths()
        {
            var recurrence = Salary(day: 5, start: "2026-10");
            recurrence.Pause();

            var result = recurrence.Resume(new DateOnly(2027, 1, 20));

            Assert.True(result.IsSuccess);
            Assert.True(recurrence.IsActive);
            Assert.Equal(Month("2027-01"), recurrence.NextMonth);
            Assert.Equal(new DateOnly(2027, 1, 5), recurrence.NextOccurrenceDate);
        }

        [Fact]
        public void Resume_ShouldKeepAFutureCursor()
        {
            var recurrence = Salary(start: "2026-12");
            recurrence.Pause();

            recurrence.Resume(Today);

            Assert.Equal(Month("2026-12"), recurrence.NextMonth);
        }

        [Fact]
        public void Resume_ShouldReturnNotPaused_WhenActive()
        {
            Assert.Equal(RecurringTransactionErrors.NotPaused, Salary().Resume(Today).Error);
        }

        [Fact]
        public void Update_ShouldChangeTheNextOccurrences_AndKeepTheCursor()
        {
            var recurrence = Salary(day: 5, start: "2026-10");
            recurrence.AdvanceAfterPosting();

            var result = recurrence.Update(MoneyFlowType.Income, 4, 9000m, "Salário novo", 20, "2026-10", "2027-06", Today);

            Assert.True(result.IsSuccess);
            Assert.Equal(4, recurrence.CategoryId.Value);
            Assert.Equal(9000m, recurrence.Amount.Value);
            Assert.Equal("salario novo", recurrence.NormalizedDescription);
            Assert.Equal(Month("2026-11"), recurrence.NextMonth);
            Assert.Equal(new DateOnly(2026, 11, 20), recurrence.NextOccurrenceDate);
            Assert.Equal(Month("2027-06"), recurrence.EndMonth);
        }

        [Fact]
        public void Update_ShouldFinish_WhenEndMonthMovesBeforeTheCursor()
        {
            var recurrence = Salary(start: "2026-10");
            recurrence.AdvanceAfterPosting();

            recurrence.Update(MoneyFlowType.Income, 2, 8000m, "Salário", 5, "2026-10", "2026-10", Today);

            Assert.Equal(RecurringTransactionStatus.Finished, recurrence.Status);
        }

        [Fact]
        public void Update_ShouldMoveTheStart_WhileItHasNotStarted()
        {
            var recurrence = Salary(start: "2026-11");

            var result = recurrence.Update(MoneyFlowType.Income, 2, 8000m, "Salário", 5, "2027-01", null, Today);

            Assert.True(result.IsSuccess);
            Assert.Equal(Month("2027-01"), recurrence.StartMonth);
            Assert.Equal(Month("2027-01"), recurrence.NextMonth);
        }

        [Fact]
        public void Update_ShouldReturnStartLocked_WhenItHasStarted()
        {
            var recurrence = Salary(start: "2026-10");
            recurrence.AdvanceAfterPosting();

            var result = recurrence.Update(MoneyFlowType.Income, 2, 8000m, "Salário", 5, "2026-12", null, Today);

            Assert.False(result.IsSuccess);
            Assert.Equal(RecurringTransactionErrors.StartLocked, result.Error);
        }

        [Fact]
        public void Update_ShouldReturnStartInPast_WhenMovingTheStartBeforeCurrentMonth()
        {
            var recurrence = Salary(start: "2026-11");

            var result = recurrence.Update(MoneyFlowType.Income, 2, 8000m, "Salário", 5, "2026-09", null, Today);

            Assert.Equal(RecurringTransactionErrors.StartInPast, result.Error);
        }

        [Fact]
        public void Update_ShouldKeepAPastStart_WhenItIsUnchanged()
        {
            var recurrence = Salary(start: "2026-10");

            var result = recurrence.Update(MoneyFlowType.Income, 2, 8000m, "Salário", 5, "2026-10", null, new DateOnly(2027, 3, 1));

            Assert.True(result.IsSuccess);
        }
    }
}
