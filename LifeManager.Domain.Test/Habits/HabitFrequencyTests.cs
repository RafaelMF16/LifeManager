using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.ValueObjects;

namespace LifeManager.Domain.Test.Habits
{
    public class HabitFrequencyTests
    {
        [Fact]
        public void Create_ShouldReturnDaily_WhenTypeIsDailyWithoutDaysOrTimes()
        {
            var result = HabitFrequency.Create(HabitFrequencyType.Daily, HabitWeekDays.None, null);

            Assert.True(result.IsSuccess);
            Assert.Equal(HabitFrequency.Daily, result.Value);
        }

        [Theory]
        [InlineData(HabitWeekDays.Monday, null)]
        [InlineData(HabitWeekDays.None, 3)]
        public void Create_ShouldReturnFailure_WhenDailyHasDaysOrTimes(HabitWeekDays weekDays, int? timesPerWeek)
        {
            var result = HabitFrequency.Create(HabitFrequencyType.Daily, weekDays, timesPerWeek);

            Assert.Equal(HabitErrors.InvalidFrequencyCombination, result.Error);
        }

        [Theory]
        [InlineData(HabitWeekDays.Monday)]
        [InlineData(HabitWeekDays.Monday | HabitWeekDays.Wednesday | HabitWeekDays.Friday)]
        [InlineData(HabitWeekDays.All)]
        public void Create_ShouldReturnWeekDays_WhenAtLeastOneDayIsPicked(HabitWeekDays weekDays)
        {
            var result = HabitFrequency.Create(HabitFrequencyType.WeekDays, weekDays, null);

            Assert.True(result.IsSuccess);
            Assert.Equal(weekDays, result.Value.WeekDays);
            Assert.Null(result.Value.TimesPerWeek);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenWeekDaysHasNoDay()
        {
            var result = HabitFrequency.Create(HabitFrequencyType.WeekDays, HabitWeekDays.None, null);

            Assert.Equal(HabitErrors.WeekDaysRequired, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenWeekDaysHasTimesPerWeek()
        {
            var result = HabitFrequency.Create(HabitFrequencyType.WeekDays, HabitWeekDays.Monday, 2);

            Assert.Equal(HabitErrors.InvalidFrequencyCombination, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenWeekDaysHasUnknownBits()
        {
            var result = HabitFrequency.Create(HabitFrequencyType.WeekDays, (HabitWeekDays)128, null);

            Assert.Equal(HabitErrors.InvalidFrequencyCombination, result.Error);
        }

        [Theory]
        [InlineData(HabitFrequency.MinTimesPerWeek)]
        [InlineData(HabitFrequency.MaxTimesPerWeek)]
        public void Create_ShouldReturnTimesPerWeek_WhenTimesAreInRange(int timesPerWeek)
        {
            var result = HabitFrequency.Create(HabitFrequencyType.TimesPerWeek, HabitWeekDays.None, timesPerWeek);

            Assert.True(result.IsSuccess);
            Assert.Equal(timesPerWeek, result.Value.TimesPerWeek);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(7)]
        [InlineData(-1)]
        public void Create_ShouldReturnFailure_WhenTimesPerWeekIsOutOfRange(int? timesPerWeek)
        {
            var result = HabitFrequency.Create(HabitFrequencyType.TimesPerWeek, HabitWeekDays.None, timesPerWeek);

            Assert.Equal(HabitErrors.InvalidTimesPerWeek, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenTimesPerWeekHasDays()
        {
            var result = HabitFrequency.Create(HabitFrequencyType.TimesPerWeek, HabitWeekDays.Monday, 3);

            Assert.Equal(HabitErrors.InvalidFrequencyCombination, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenTypeIsUndefined()
        {
            var result = HabitFrequency.Create((HabitFrequencyType)99, HabitWeekDays.None, null);

            Assert.Equal(HabitErrors.InvalidFrequencyType, result.Error);
        }

        [Fact]
        public void IsScheduledOn_ShouldOnlyMatchPickedDays_WhenTypeIsWeekDays()
        {
            var frequency = HabitFrequency.Create(HabitFrequencyType.WeekDays, HabitWeekDays.Monday | HabitWeekDays.Sunday, null).Value!;

            Assert.True(frequency.IsScheduledOn(new DateOnly(2026, 10, 5)));   // Monday
            Assert.False(frequency.IsScheduledOn(new DateOnly(2026, 10, 6)));  // Tuesday
            Assert.True(frequency.IsScheduledOn(new DateOnly(2026, 10, 11)));  // Sunday
        }

        [Fact]
        public void IsScheduledOn_ShouldMatchEveryDay_WhenTypeIsDailyOrTimesPerWeek()
        {
            var timesPerWeek = HabitFrequency.Create(HabitFrequencyType.TimesPerWeek, HabitWeekDays.None, 3).Value!;

            for (var date = new DateOnly(2026, 10, 5); date <= new DateOnly(2026, 10, 11); date = date.AddDays(1))
            {
                Assert.True(HabitFrequency.Daily.IsScheduledOn(date));
                Assert.True(timesPerWeek.IsScheduledOn(date));
            }
        }
    }
}
