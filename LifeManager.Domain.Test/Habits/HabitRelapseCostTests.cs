using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.ValueObjects;

namespace LifeManager.Domain.Test.Habits
{
    public class HabitRelapseCostTests
    {
        [Fact]
        public void Create_ShouldReturnNull_WhenValueIsNull()
        {
            var result = HabitRelapseCost.Create(null);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value);
        }

        [Fact]
        public void Create_ShouldKeepTheValue_WhenItIsValid()
        {
            var result = HabitRelapseCost.Create(45.90m);

            Assert.Equal(45.90m, result.Value!.Value);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Create_ShouldReturnNotPositive_WhenValueIsZeroOrNegative(decimal value)
        {
            var result = HabitRelapseCost.Create(value);

            Assert.Equal(HabitErrors.RelapseCostNotPositive, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnTooManyDecimals_WhenValueHasThreePlaces()
        {
            var result = HabitRelapseCost.Create(1.234m);

            Assert.Equal(HabitErrors.RelapseCostTooManyDecimals, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnTooLarge_WhenValueDoesNotFitTheColumn()
        {
            var result = HabitRelapseCost.Create(HabitRelapseCost.MaxValue + 0.01m);

            Assert.Equal(HabitErrors.RelapseCostTooLarge, result.Error);
        }
    }
}
