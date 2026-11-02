using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Challenges;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using static LifeManager.Domain.Test.Habits.Challenges.ChallengeTestData;

namespace LifeManager.Domain.Test.Habits.Challenges
{
    public class ChallengeRulesTests
    {
        [Theory]
        [InlineData(0, true)]   // Monday
        [InlineData(2, true)]   // Wednesday
        [InlineData(3, false)]  // Thursday
        [InlineData(6, false)]  // Sunday
        public void EnsureWeekIsOpen_ShouldTakeTheCurrentWeekUntilWednesday(int dayOffset, bool open)
        {
            var result = ChallengeRules.EnsureWeekIsOpen(Monday, Monday.AddDays(dayOffset));

            Assert.Equal(open, result.IsSuccess);
            if (!open)
                Assert.Equal(ChallengeErrors.CreationWindowClosed, result.Error);
        }

        [Fact]
        public void EnsureWeekIsOpen_ShouldAlwaysTakeTheNextWeek_AndNeverAPastOrLaterOne()
        {
            var sunday = Monday.AddDays(6);

            Assert.True(ChallengeRules.EnsureWeekIsOpen(Monday.AddDays(7), sunday).IsSuccess);
            Assert.False(ChallengeRules.EnsureWeekIsOpen(Monday.AddDays(-7), Monday).IsSuccess);
            Assert.False(ChallengeRules.EnsureWeekIsOpen(Monday.AddDays(14), Monday).IsSuccess);
            Assert.Equal(ChallengeErrors.InvalidWeek, ChallengeRules.EnsureWeekIsOpen(Monday.AddDays(1), Monday).Error);
        }

        [Fact]
        public void WeekStartFor_ShouldPickThisMondayOrTheNextOne()
        {
            Assert.Equal(Monday, ChallengeRules.WeekStartFor(ChallengeWeek.Current, Monday.AddDays(4)));
            Assert.Equal(Monday.AddDays(7), ChallengeRules.WeekStartFor(ChallengeWeek.Next, Monday.AddDays(4)));
        }

        [Fact]
        public void PossibleDays_ShouldFollowTheFrequency_AndTheStartDate()
        {
            Assert.Equal(7, ChallengeRules.PossibleDays(Habit(), Monday));
            Assert.Equal(7, ChallengeRules.PossibleDays(Habit(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 3), Monday));
            Assert.Equal(3, ChallengeRules.PossibleDays(
                Habit(frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Monday | HabitWeekDays.Wednesday | HabitWeekDays.Friday), Monday));
            Assert.Equal(2, ChallengeRules.PossibleDays(Habit(startDate: Monday.AddDays(5)), Monday));
        }

        [Fact]
        public void MaxFrequencyTarget_ShouldCountOnlyDaysStillOpen_InTheCurrentWeek()
        {
            var habit = Habit();
            // Thursday: Wednesday (yesterday) to Sunday are open; Monday was done.
            var thursday = Monday.AddDays(3);

            Assert.Equal(6, ChallengeRules.MaxFrequencyTarget(habit, Monday, thursday, [Monday]));
            Assert.Equal(7, ChallengeRules.MaxFrequencyTarget(habit, Monday.AddDays(7), thursday, []));
        }

        [Fact]
        public void MaxVolumeTarget_ShouldBeThreeTimesAWeekOfTheDailyTarget()
        {
            Assert.Equal(7 * 2000 * 3, ChallengeRules.MaxVolumeTarget(Habit(targetValue: 2000)));
        }

        [Fact]
        public void ClosedWeeks_ShouldTakeUpToFourEndedWeeks_SinceTheHabitStarted()
        {
            Assert.Equal(
                [Monday.AddDays(-7), Monday.AddDays(-14), Monday.AddDays(-21), Monday.AddDays(-28)],
                ChallengeRules.ClosedWeeks(Monday.AddDays(2), Monday.AddDays(-60)));

            // Started on a Thursday two weeks ago: that partial week would pull the average down, so only whole weeks count.
            Assert.Equal([Monday.AddDays(-7)], ChallengeRules.ClosedWeeks(Monday.AddDays(2), Monday.AddDays(-11)));

            // Started on a Monday: that week is whole.
            Assert.Equal([Monday.AddDays(-7), Monday.AddDays(-14)], ChallengeRules.ClosedWeeks(Monday.AddDays(2), Monday.AddDays(-14)));

            Assert.Empty(ChallengeRules.ClosedWeeks(Monday.AddDays(2), Monday));
        }

        [Fact]
        public void Baseline_ShouldAverageTheWeeks_OrBeNullWithoutAny()
        {
            Assert.Equal(3.5m, ChallengeRules.Baseline([3, 4]));
            Assert.Equal(3.33m, ChallengeRules.Baseline([3, 3, 4]));
            Assert.Null(ChallengeRules.Baseline([]));
        }

        [Theory]
        // Without an average there is nothing to beat: light, so a brand-new habit can't farm the bigger rewards.
        [InlineData(ChallengeType.Frequency, 3, null, ChallengeEffort.Light)]
        [InlineData(ChallengeType.Volume, 5000, null, ChallengeEffort.Light)]
        [InlineData(ChallengeType.Frequency, 3, 3.0, ChallengeEffort.Light)]
        [InlineData(ChallengeType.Frequency, 4, 3.0, ChallengeEffort.Steady)]
        [InlineData(ChallengeType.Frequency, 5, 3.0, ChallengeEffort.Stretch)]
        [InlineData(ChallengeType.Volume, 1000, 1000.0, ChallengeEffort.Light)]
        [InlineData(ChallengeType.Volume, 1250, 1000.0, ChallengeEffort.Steady)]
        [InlineData(ChallengeType.Volume, 1251, 1000.0, ChallengeEffort.Stretch)]
        public void Effort_ShouldCompareTheTargetWithTheBaseline(ChallengeType type, int target, double? baseline, ChallengeEffort expected)
        {
            Assert.Equal(expected, ChallengeRules.Effort(type, target, (decimal?)baseline));
        }

        [Theory]
        [InlineData(ChallengeEffort.Light, 20, 40)]
        [InlineData(ChallengeEffort.Steady, 40, 80)]
        [InlineData(ChallengeEffort.Stretch, 60, 120)]
        public void Reward_ShouldMultiplyTheHabitsReward_ByTheEffort(ChallengeEffort effort, int coins, int xp)
        {
            Assert.Equal(new ChallengeReward(coins, xp), ChallengeRules.Reward(HabitDifficulty.Medium, effort));
        }
    }
}
