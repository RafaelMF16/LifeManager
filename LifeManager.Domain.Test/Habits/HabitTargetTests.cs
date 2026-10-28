using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.ValueObjects;

namespace LifeManager.Domain.Test.Habits
{
    public class HabitTargetTests
    {
        [Fact]
        public void Create_ShouldReturnNull_WhenEverythingIsNull()
        {
            var result = HabitTarget.Create(null, null, null);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value);
        }

        [Fact]
        public void Create_ShouldReturnNull_WhenOnlyABlankUnitComes()
        {
            var result = HabitTarget.Create(null, "  ", null);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value);
        }

        [Fact]
        public void Create_ShouldTrimTheUnit_WhenTargetIsValid()
        {
            var target = HabitTarget.Create(2000, "  ml ", 250).Value!;

            Assert.Equal(2000, target.Value);
            Assert.Equal("ml", target.Unit);
            Assert.Equal(250, target.Step);
        }

        [Fact]
        public void Create_ShouldAcceptNoStep()
        {
            var target = HabitTarget.Create(20, "pages", null).Value!;

            Assert.Null(target.Step);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(HabitTarget.MaxValue, HabitTarget.MaxValue)]
        public void Create_ShouldAcceptTheLimits(int value, int step)
        {
            Assert.True(HabitTarget.Create(value, "x", step).IsSuccess);
        }

        [Fact]
        public void Create_ShouldAcceptAUnitOfMaxLength()
        {
            Assert.True(HabitTarget.Create(10, new string('a', HabitTarget.UnitMaxLength), null).IsSuccess);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(HabitTarget.MaxValue + 1)]
        public void Create_ShouldReturnFailure_WhenValueIsOutOfRange(int value)
        {
            Assert.Equal(HabitErrors.InvalidTargetValue, HabitTarget.Create(value, "ml", null).Error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_ShouldReturnFailure_WhenUnitIsMissing(string? unit)
        {
            Assert.Equal(HabitErrors.TargetUnitRequired, HabitTarget.Create(10, unit, null).Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenUnitIsTooLong()
        {
            Assert.Equal(HabitErrors.TargetUnitTooLong, HabitTarget.Create(10, new string('a', HabitTarget.UnitMaxLength + 1), null).Error);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(11)]
        public void Create_ShouldReturnFailure_WhenStepIsOutOfRange(int step)
        {
            Assert.Equal(HabitErrors.InvalidTargetStep, HabitTarget.Create(10, "pages", step).Error);
        }

        [Theory]
        [InlineData("ml", null)]
        [InlineData(null, 250)]
        public void Create_ShouldReturnFailure_WhenUnitOrStepComeWithoutValue(string? unit, int? step)
        {
            Assert.Equal(HabitErrors.InvalidTargetCombination, HabitTarget.Create(null, unit, step).Error);
        }
    }
}
