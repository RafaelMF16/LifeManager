using LifeManager.Domain.Habits;

namespace LifeManager.Domain.Test.Habits
{
    public class HabitSavingsTests
    {
        [Fact]
        public void Saved_ShouldMultiplyTheCleanDaysByTheCost()
        {
            Assert.Equal(135m, HabitSavings.Saved(45m, 3));
        }

        [Fact]
        public void Saved_ShouldReturnNull_WhenTheHabitHasNoCost()
        {
            Assert.Null(HabitSavings.Saved(null, 3));
        }

        [Fact]
        public void Saved_ShouldReturnZero_WhenThereAreNoCleanDays()
        {
            Assert.Equal(0m, HabitSavings.Saved(45m, 0));
        }

        [Fact]
        public void ForPeriod_ShouldSumAndOrderByAmountThenNameThenId()
        {
            var summary = HabitSavings.ForPeriod(
            [
                new HabitCleanDays(3, "Cigarro", 15m, 2),
                new HabitCleanDays(1, "Delivery", 45m, 2),
                new HabitCleanDays(4, "Bar", 10m, 3),
                new HabitCleanDays(2, "Apostas", 30m, 1)
            ]);

            Assert.Equal(180m, summary.Total);
            Assert.Equal([1, 2, 4, 3], summary.Habits.Select(item => item.HabitId));
            Assert.Equal(90m, summary.Habits[0].Amount);
            Assert.Equal(2, summary.Habits[0].CleanDays);
        }

        [Fact]
        public void ForPeriod_ShouldReturnZero_WhenThereIsNothing()
        {
            var summary = HabitSavings.ForPeriod([]);

            Assert.Equal(0m, summary.Total);
            Assert.Empty(summary.Habits);
        }
    }
}
