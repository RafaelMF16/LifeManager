using LifeManager.Domain.MonthlySummaries;

namespace LifeManager.Domain.Test.MonthlySummaries
{
    public class MonthlySummaryTests
    {
        [Fact]
        public void Create_ShouldReturnMonthlySummary_WhenValuesAreValid()
        {
            var totalIncome = 100;
            var totalExpense = 50;
            var userId = 1;
            var month = 11;
            var year = DateTimeOffset.UtcNow.Year;
            var monthlySummary = MonthlySummary.Create(totalIncome, totalExpense, userId, month, year);

            Assert.NotNull(monthlySummary);
            Assert.IsType<MonthlySummary>(monthlySummary);
            Assert.Null(monthlySummary.Id);
            Assert.Equal(totalIncome, monthlySummary.TotalIncome.Value);
            Assert.Equal(totalExpense, monthlySummary.TotalExpense.Value);
            Assert.Equal(userId, monthlySummary.UserId.Value);
            Assert.Equal(month, monthlySummary.Month.Value);
            Assert.Equal(year, monthlySummary.Year.Value);
        }

        [Fact]
        public void AssignId_ShouldSetId_WhenMonthlySummaryHasNoIdYet()
        {
            var monthlySummary = MonthlySummary.Create(100, 50, 1, 11, DateTimeOffset.UtcNow.Year);

            monthlySummary.AssignId(10);

            Assert.NotNull(monthlySummary.Id);
            Assert.Equal(10, monthlySummary.Id!.Value);
        }
    }
}