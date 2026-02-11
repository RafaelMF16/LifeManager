using LifeManager.Domain.MonthlySummaries;

namespace LifeManager.Domain.Test.MonthlySummaries
{
    public class MonthlySummaryTests
    {
        [Fact]
        public void Create_ShouldReturnMonthlySummary_WhenValuesAreValid()
        {
            var id = 1;
            var totalIncome = 100;
            var totalExpense = 50;
            var userId = 1;
            var month = 11;
            var year = DateTimeOffset.UtcNow.Year;
            var monthlySummary = MonthlySummary.Create(id, totalIncome, totalExpense, userId, month, year);

            Assert.NotNull(monthlySummary);
            Assert.IsType<MonthlySummary>(monthlySummary);
            Assert.Equal(id, monthlySummary.Id.Value);
            Assert.Equal(totalIncome, monthlySummary.TotalIncome.Value);
            Assert.Equal(totalExpense, monthlySummary.TotalExpense.Value);
            Assert.Equal(userId, monthlySummary.UserId.Value);
            Assert.Equal(month, monthlySummary.Month.Value);
            Assert.Equal(year, monthlySummary.Year.Value);
        }
    }
}