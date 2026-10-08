using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Test.Habits
{
    public class HabitDayVerdictTests
    {
        // Week from Monday 2026-09-28 to Sunday 2026-10-04.
        private static readonly DateOnly Monday = new(2026, 9, 28);
        private static readonly DateOnly Wednesday = new(2026, 9, 30);
        private static readonly DateOnly Sunday = new(2026, 10, 4);
        private static readonly DateOnly StartDate = new(2026, 9, 1);
        private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

        private static Habit Habit(
            HabitKind kind = HabitKind.Positive,
            HabitFrequencyType frequencyType = HabitFrequencyType.Daily,
            HabitWeekDays weekDays = HabitWeekDays.None,
            int? timesPerWeek = null,
            DateOnly? startDate = null)
            => Domain.Habits.Habit.FromPersistence(
                1, 1, "Habit", null, null, kind, HabitDifficulty.Easy, frequencyType, weekDays, timesPerWeek,
                startDate ?? StartDate, Now, null, 0, 0, (startDate ?? StartDate).AddDays(-1));

        private static HabitCheckIn CheckIn(Habit habit, DateOnly date, HabitCheckInStatus status = HabitCheckInStatus.Done)
            => HabitCheckIn.Judged(habit, date, status, Now, GameDelta.None);

        [Fact]
        public void Judge_ShouldBeMissed_WhenAHabitToBuildWasDueAndNotDone()
        {
            Assert.Equal(HabitDayVerdictKind.Missed, HabitDayVerdict.Judge(Habit(), Wednesday, []).Kind);
        }

        [Fact]
        public void Judge_ShouldBeSettled_WhenTheDayHasACheckIn()
        {
            var habit = Habit();

            Assert.Equal(HabitDayVerdictKind.Settled, HabitDayVerdict.Judge(habit, Wednesday, [CheckIn(habit, Wednesday)]).Kind);
        }

        [Fact]
        public void Judge_ShouldIgnoreOtherDaysCheckIns()
        {
            var habit = Habit();

            Assert.Equal(HabitDayVerdictKind.Missed, HabitDayVerdict.Judge(habit, Wednesday, [CheckIn(habit, Monday)]).Kind);
        }

        [Fact]
        public void Judge_ShouldSkip_ADayBeforeTheStartDate()
        {
            Assert.Equal(HabitDayVerdictKind.Skip, HabitDayVerdict.Judge(Habit(startDate: Sunday), Wednesday, []).Kind);
        }

        [Fact]
        public void Judge_ShouldSkip_ADayTheHabitIsNotScheduledOn()
        {
            var habit = Habit(frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Monday);

            Assert.Equal(HabitDayVerdictKind.Skip, HabitDayVerdict.Judge(habit, Wednesday, []).Kind);
            Assert.Equal(HabitDayVerdictKind.Missed, HabitDayVerdict.Judge(habit, Monday, []).Kind);
        }

        [Fact]
        public void Judge_ShouldBeClean_WhenAHabitToAvoidHadNoRelapse()
        {
            Assert.Equal(HabitDayVerdictKind.Clean, HabitDayVerdict.Judge(Habit(kind: HabitKind.Negative), Wednesday, []).Kind);
        }

        [Fact]
        public void Judge_ShouldSettle_AHabitToAvoidWithARelapse()
        {
            var habit = Habit(kind: HabitKind.Negative);

            Assert.Equal(HabitDayVerdictKind.Settled, HabitDayVerdict.Judge(habit, Wednesday, [CheckIn(habit, Wednesday, HabitCheckInStatus.Relapse)]).Kind);
        }

        [Fact]
        public void Judge_ShouldSkip_TheFreeDaysOfAHabitToAvoid()
        {
            // Avoided Monday to Friday: the weekend is free.
            var habit = Habit(kind: HabitKind.Negative, frequencyType: HabitFrequencyType.WeekDays,
                weekDays: HabitWeekDays.Monday | HabitWeekDays.Tuesday | HabitWeekDays.Wednesday | HabitWeekDays.Thursday | HabitWeekDays.Friday);

            Assert.Equal(HabitDayVerdictKind.Skip, HabitDayVerdict.Judge(habit, Sunday, []).Kind);
            Assert.Equal(HabitDayVerdictKind.Clean, HabitDayVerdict.Judge(habit, Wednesday, []).Kind);
        }

        [Fact]
        public void Judge_ShouldSkip_AWeeklyHabitBeforeItsSunday()
        {
            var habit = Habit(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 3);

            Assert.Equal(HabitDayVerdictKind.Skip, HabitDayVerdict.Judge(habit, Wednesday, []).Kind);
        }

        [Fact]
        public void Judge_ShouldCountTheShortfall_WhenAWeeklyHabitsWeekClosesShort()
        {
            var habit = Habit(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 3);

            var verdict = HabitDayVerdict.Judge(habit, Sunday, [CheckIn(habit, Monday)]);

            Assert.Equal(HabitDayVerdictKind.WeekShort, verdict.Kind);
            Assert.Equal(2, verdict.Shortfall);
        }

        [Fact]
        public void Judge_ShouldSettle_AWeeklyHabitThatMetItsTarget()
        {
            var habit = Habit(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);

            Assert.Equal(HabitDayVerdictKind.Settled, HabitDayVerdict.Judge(habit, Sunday, [CheckIn(habit, Monday), CheckIn(habit, Wednesday)]).Kind);
        }

        [Fact]
        public void Judge_ShouldSkip_AWeekThatStartedBeforeTheHabit()
        {
            var habit = Habit(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 3, startDate: Wednesday);

            Assert.Equal(HabitDayVerdictKind.Skip, HabitDayVerdict.Judge(habit, Sunday, []).Kind);
        }

        [Fact]
        public void Judge_ShouldSkip_AWeeklyLimitBeforeItsSunday()
        {
            var habit = Habit(kind: HabitKind.Negative, frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);

            Assert.Equal(HabitDayVerdictKind.Skip, HabitDayVerdict.Judge(habit, Wednesday, []).Kind);
        }

        [Fact]
        public void Judge_ShouldMakeEveryRelapseFreeDayClean_WhenAWeeklyLimitWasKept()
        {
            var habit = Habit(kind: HabitKind.Negative, frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);

            var verdict = HabitDayVerdict.Judge(habit, Sunday, [CheckIn(habit, Monday, HabitCheckInStatus.Relapse), CheckIn(habit, Wednesday, HabitCheckInStatus.Relapse)]);

            Assert.Equal(HabitDayVerdictKind.WeekClean, verdict.Kind);
            Assert.Equal(5, verdict.CleanDays!.Count);
            Assert.DoesNotContain(Monday, verdict.CleanDays);
            Assert.DoesNotContain(Wednesday, verdict.CleanDays);
        }

        [Fact]
        public void Judge_ShouldSettle_AWeeklyLimitThatWasPassed()
        {
            var habit = Habit(kind: HabitKind.Negative, frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 1);

            var verdict = HabitDayVerdict.Judge(habit, Sunday, [CheckIn(habit, Monday, HabitCheckInStatus.Relapse), CheckIn(habit, Wednesday, HabitCheckInStatus.Relapse)]);

            Assert.Equal(HabitDayVerdictKind.Settled, verdict.Kind);
        }
    }
}
