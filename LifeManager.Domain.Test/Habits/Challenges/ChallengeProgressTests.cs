using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Challenges;
using LifeManager.Domain.Habits.Enums;
using static LifeManager.Domain.Test.Habits.Challenges.ChallengeTestData;

namespace LifeManager.Domain.Test.Habits.Challenges
{
    public class ChallengeProgressTests
    {
        [Fact]
        public void Frequency_ShouldCountOnlyDoneDaysOfTheWeek()
        {
            var checkIns = new (DateOnly, HabitCheckInStatus)[]
            {
                (Monday, HabitCheckInStatus.Done),
                (Monday.AddDays(1), HabitCheckInStatus.Frozen),
                (Monday.AddDays(2), HabitCheckInStatus.Missed),
                (Monday.AddDays(3), HabitCheckInStatus.Done),
                (Monday.AddDays(-1), HabitCheckInStatus.Done),
                (Monday.AddDays(7), HabitCheckInStatus.Done)
            };

            Assert.Equal(2, ChallengeProgress.Frequency(Monday, checkIns));
        }

        [Fact]
        public void Volume_ShouldSumEveryEntryOfTheWeek_EvenOnDaysShortOfTheTarget()
        {
            var entries = new (DateOnly, int)[] { (Monday, 500), (Monday, 250), (Monday.AddDays(6), 100), (Monday.AddDays(7), 900) };

            Assert.Equal(850, ChallengeProgress.Volume(Monday, entries));
        }

        [Theory]
        [InlineData(0, 7)]
        [InlineData(4, 3)]
        [InlineData(6, 1)]
        [InlineData(7, 0)]
        public void DaysLeft_ShouldCountTodayUntilSunday(int dayOffset, int expected)
        {
            Assert.Equal(expected, ChallengeProgress.DaysLeft(Monday, Monday.AddDays(dayOffset)));
        }

        [Fact]
        public void DaysLeft_ShouldBeTheWholeWeek_ForTheNextOne()
        {
            Assert.Equal(7, ChallengeProgress.DaysLeft(Monday.AddDays(7), Monday.AddDays(3)));
        }

        [Fact]
        public void IsOver_ShouldWaitUntilTuesday_SinceSundayCanStillChangeOnMonday()
        {
            Assert.False(ChallengeProgress.IsOver(Monday, Monday.AddDays(7)));
            Assert.True(ChallengeProgress.IsOver(Monday, Monday.AddDays(8)));
        }
    }
}
