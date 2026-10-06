using LifeManager.Domain.RecurringTransactions.Errors;
using LifeManager.Domain.RecurringTransactions.ValueObjects;
using LifeManager.Domain.Shared.ValueObjects;

namespace LifeManager.Domain.Test.RecurringTransactions
{
    public class RecurrenceDayTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(15)]
        [InlineData(31)]
        public void Create_ShouldReturnDay_WhenBetweenOneAndThirtyOne(int value)
        {
            var result = RecurrenceDay.Create(value);

            Assert.True(result.IsSuccess);
            Assert.Equal(value, result.Value.Value);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(32)]
        public void Create_ShouldReturnInvalidDay_WhenOutOfRange(int value)
        {
            Assert.Equal(RecurringTransactionErrors.InvalidDay, RecurrenceDay.Create(value).Error);
        }

        [Theory]
        [InlineData(30, "2026-02", 28)]
        [InlineData(29, "2028-02", 29)]
        [InlineData(31, "2026-04", 30)]
        [InlineData(15, "2026-02", 15)]
        public void DateIn_ShouldClampToTheMonthLength(int day, string month, int expectedDay)
        {
            YearMonth.TryParse(month, out var yearMonth);

            var date = RecurrenceDay.Create(day).Value!.DateIn(yearMonth);

            Assert.Equal(new DateOnly(yearMonth.Year, yearMonth.Month, expectedDay), date);
        }
    }
}
