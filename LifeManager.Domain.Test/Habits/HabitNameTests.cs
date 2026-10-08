using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.ValueObjects;

namespace LifeManager.Domain.Test.Habits
{
    public class HabitNameTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_ShouldReturnFailure_WhenValueIsNullOrWhiteSpace(string? value)
        {
            var result = HabitName.Create(value);

            Assert.False(result.IsSuccess);
            Assert.Equal(HabitErrors.NameIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenValueIsLongerThanMaxLength()
        {
            var result = HabitName.Create(new string('a', HabitName.MaxLength + 1));

            Assert.False(result.IsSuccess);
            Assert.Equal(HabitErrors.NameTooLong, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnHabitName_WhenValueHasExactlyMaxLength()
        {
            var name = new string('a', HabitName.MaxLength);

            var result = HabitName.Create(name);

            Assert.True(result.IsSuccess);
            Assert.Equal(name, result.Value.Value);
        }

        [Fact]
        public void Create_ShouldTrimValueAndNormalize_WhenValueHasAccentsAndSurroundingWhiteSpace()
        {
            var result = HabitName.Create("  Meditação  ");

            Assert.True(result.IsSuccess);
            Assert.Equal("Meditação", result.Value.Value);
            Assert.Equal("meditacao", result.Value.NormalizedValue);
        }
    }
}
