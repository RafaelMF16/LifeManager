using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.ValueObjects;

namespace LifeManager.Domain.Test.Habits
{
    public class HabitOptionalTextTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_ShouldReturnNull_WhenValueIsNullOrWhiteSpace(string? value)
        {
            var description = HabitDescription.Create(value);
            var trigger = HabitTrigger.Create(value);

            Assert.True(description.IsSuccess);
            Assert.Null(description.Value);
            Assert.True(trigger.IsSuccess);
            Assert.Null(trigger.Value);
        }

        [Fact]
        public void Create_ShouldTrimValue_WhenValueHasSurroundingWhiteSpace()
        {
            var description = HabitDescription.Create("  Read before bed  ");
            var trigger = HabitTrigger.Create("  After brushing my teeth  ");

            Assert.Equal("Read before bed", description.Value!.Value);
            Assert.Equal("After brushing my teeth", trigger.Value!.Value);
        }

        [Fact]
        public void Create_ShouldAcceptValue_WhenValueHasExactlyMaxLength()
        {
            Assert.True(HabitDescription.Create(new string('a', HabitDescription.MaxLength)).IsSuccess);
            Assert.True(HabitTrigger.Create(new string('a', HabitTrigger.MaxLength)).IsSuccess);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenValueIsLongerThanMaxLength()
        {
            var description = HabitDescription.Create(new string('a', HabitDescription.MaxLength + 1));
            var trigger = HabitTrigger.Create(new string('a', HabitTrigger.MaxLength + 1));

            Assert.Equal(HabitErrors.DescriptionTooLong, description.Error);
            Assert.Equal(HabitErrors.TriggerTooLong, trigger.Error);
        }
    }
}
