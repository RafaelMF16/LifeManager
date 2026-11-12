using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Test.Habits
{
    public class HabitRemindersTests
    {
        // Wednesday.
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateOnly StartDate = new(2026, 9, 1);
        private static readonly DateTimeOffset Now = new(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);
        private const int Hour = 20;

        private static readonly IReadOnlyDictionary<int, int> NoProgress = new Dictionary<int, int>();

        private static Habit Stored(
            int id,
            string? name = null,
            int? reminderHour = Hour,
            HabitFrequencyType frequencyType = HabitFrequencyType.Daily,
            HabitWeekDays weekDays = HabitWeekDays.None,
            int? timesPerWeek = null,
            int? targetValue = null,
            string? targetUnit = null)
            => Habit.FromPersistence(
                id, 1, name ?? $"Habit {id}", null, null, HabitKind.Positive, HabitDifficulty.Easy, frequencyType, weekDays, timesPerWeek,
                StartDate, Now, archivedAt: null, currentStreak: 0, longestStreak: 0, evaluatedUntil: Today.AddDays(-2),
                targetValue: targetValue, targetUnit: targetUnit, reminderHour: reminderHour);

        private static HabitCheckIn CheckIn(int habitId, DateOnly date)
            => HabitCheckIn.FromPersistence(habitId * 100 + date.Day, habitId, 1, date, HabitCheckInStatus.Done, Now, 0, 0, 0);

        [Fact]
        public void Due_ShouldRemindAHabitOfThisHourThatIsNotDone()
        {
            var agenda = HabitDayAgenda.For([Stored(1, "Ler")], [], [], Today);

            var reminder = Assert.Single(HabitReminders.Due(agenda, Hour, NoProgress, Today));

            Assert.Equal("Ler", reminder.Habit.Name.Value);
            Assert.Null(reminder.Remaining);
        }

        [Fact]
        public void Due_ShouldLeaveOutHabitsOfAnotherHourOrWithoutReminder()
        {
            var agenda = HabitDayAgenda.For([Stored(1, reminderHour: Hour - 1), Stored(2, reminderHour: null)], [], [], Today);

            Assert.Empty(HabitReminders.Due(agenda, Hour, NoProgress, Today));
        }

        [Fact]
        public void Due_ShouldLeaveOutAHabitAlreadyDoneToday()
        {
            var agenda = HabitDayAgenda.For([Stored(1)], [], [CheckIn(1, Today)], Today);

            Assert.Empty(HabitReminders.Due(agenda, Hour, NoProgress, Today));
        }

        [Fact]
        public void Due_ShouldLeaveOutAHabitOnItsDayOff()
        {
            var agenda = HabitDayAgenda.For([Stored(1, frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Monday)], [], [], Today);

            Assert.Empty(HabitReminders.Due(agenda, Hour, NoProgress, Today));
        }

        [Fact]
        public void Due_ShouldLeaveOutATimesPerWeekHabitWhoseWeekTargetIsMet()
        {
            // Twice a week, done on Monday and Tuesday of this week.
            var habit = Stored(1, frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);
            var agenda = HabitDayAgenda.For([habit], [], [CheckIn(1, Today.AddDays(-2)), CheckIn(1, Today.AddDays(-1))], Today);

            Assert.Empty(HabitReminders.Due(agenda, Hour, NoProgress, Today));
        }

        [Fact]
        public void Due_ShouldRemindATimesPerWeekHabitShortOfItsWeekTarget()
        {
            var habit = Stored(1, frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 3);
            var agenda = HabitDayAgenda.For([habit], [], [CheckIn(1, Today.AddDays(-1))], Today);

            Assert.Single(HabitReminders.Due(agenda, Hour, NoProgress, Today));
        }

        [Fact]
        public void Due_ShouldTellHowMuchIsLeft_ForAHabitWithADailyTarget()
        {
            var habit = Stored(1, "Beber água", targetValue: 2000, targetUnit: "ml");
            var agenda = HabitDayAgenda.For([habit], [], [], Today);

            var reminder = Assert.Single(HabitReminders.Due(agenda, Hour, new Dictionary<int, int> { [1] = 1250 }, Today));

            Assert.Equal(750, reminder.Remaining);
            Assert.Equal("ml", reminder.Unit);
        }

        [Fact]
        public void Due_ShouldTellTheWholeTarget_WhenNothingWasLoggedToday()
        {
            var habit = Stored(1, "Beber água", targetValue: 2000, targetUnit: "ml");

            var reminder = Assert.Single(HabitReminders.Due(HabitDayAgenda.For([habit], [], [], Today), Hour, NoProgress, Today));

            Assert.Equal(2000, reminder.Remaining);
        }

        [Fact]
        public void Due_ShouldOrderTheHabitsByName()
        {
            var agenda = HabitDayAgenda.For([Stored(1, "Yoga"), Stored(2, "Água"), Stored(3, "academia")], [], [], Today);

            var names = HabitReminders.Due(agenda, Hour, NoProgress, Today).Select(reminder => reminder.Habit.Name.Value);

            Assert.Equal(["academia", "Água", "Yoga"], names);
        }
    }
}
