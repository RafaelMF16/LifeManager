using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Test.Habits
{
    public class HabitRewardsTests
    {
        private static readonly DateOnly StartDate = new(2026, 9, 1);

        private static Habit Habit(HabitFrequencyType frequencyType = HabitFrequencyType.Daily, int? timesPerWeek = null)
            => Domain.Habits.Habit.FromPersistence(
                1, 1, "Habit", null, null, HabitKind.Positive, HabitDifficulty.Easy, frequencyType, HabitWeekDays.None, timesPerWeek,
                StartDate, DateTimeOffset.UnixEpoch, null, 0, 0, StartDate.AddDays(-1));

        [Fact]
        public void StreakGain_ShouldPayTheMilestoneAndAFreeze_OnTheSeventhDay()
        {
            Assert.Equal(new StreakGain(25, 1, 7), HabitRewards.StreakGain(Habit(), 6, 7));
        }

        [Fact]
        public void StreakGain_ShouldBeNone_WhenTheStreakDidNotGrow()
        {
            Assert.Equal(new StreakGain(0, 0, null), HabitRewards.StreakGain(Habit(), 7, 7));
        }

        [Fact]
        public void StreakGain_ShouldCountAWeekAsSevenDays_ForAWeeklyHabit()
        {
            var weekly = Habit(HabitFrequencyType.TimesPerWeek, 3);

            Assert.Equal(new StreakGain(25, 1, 7), HabitRewards.StreakGain(weekly, 0, 1));
            // Week 4 → 5 is day 28 → 35: passes the 30-day milestone.
            Assert.Equal(new StreakGain(100, 1, 30), HabitRewards.StreakGain(weekly, 4, 5));
        }

        [Fact]
        public void ApplyCompletion_ShouldAddTheMilestoneOnItsOwnLedgerLine_AndCapTheFreezes()
        {
            var habit = Habit();
            var profile = PlayerProfile.FromPersistence(1, 1, 0, GameRules.MaxHp, GameRules.MaxHp, 0, GameRules.MaxStreakFreezes - 1);
            var date = new DateOnly(2026, 10, 7);

            // 0 → 14 passes the 7-day milestone and two multiples of 7, but only one freeze fits.
            var completion = HabitRewards.ApplyCompletion(habit, profile, GameLedgerEntryKind.HabitDone, date, DateTimeOffset.UnixEpoch, 0, 14, new HashSet<DateOnly> { date });

            Assert.Equal(1, completion.FreezesEarned);
            Assert.Equal(GameRules.MaxStreakFreezes, profile.StreakFreezes);
            Assert.Equal(25, completion.MilestoneCoins);
            Assert.Equal([GameLedgerEntryKind.HabitDone, GameLedgerEntryKind.StreakMilestone], completion.Entries.Select(entry => entry.Kind));
            Assert.Equal(profile.Coins, completion.Applied.Coins);
        }
    }
}
