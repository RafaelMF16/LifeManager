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
    }
}
