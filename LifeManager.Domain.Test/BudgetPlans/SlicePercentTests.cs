using LifeManager.Domain.BudgetPlans.Errors;
using LifeManager.Domain.BudgetPlans.ValueObjects;

namespace LifeManager.Domain.Test.BudgetPlans
{
    public class SlicePercentTests
    {
        [Theory]
        [InlineData(0.01)]
        [InlineData(25)]
        [InlineData(33.33)]
        [InlineData(100)]
        public void Create_ShouldAccept_WhenBetweenAHundredthAndAHundred(decimal value)
        {
            var result = SlicePercent.Create(value);

            Assert.True(result.IsSuccess);
            Assert.Equal(value, result.Value.Value);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(100.01)]
        public void Create_ShouldReturnOutOfRange_WhenNotAboveZeroAndUpToAHundred(decimal value)
        {
            Assert.Equal(BudgetPlanErrors.PercentOutOfRange, SlicePercent.Create(value).Error);
        }

        [Fact]
        public void Create_ShouldReturnTooManyDecimals_WhenMoreThanTwoPlaces()
        {
            Assert.Equal(BudgetPlanErrors.PercentTooManyDecimals, SlicePercent.Create(12.345m).Error);
        }
    }
}
