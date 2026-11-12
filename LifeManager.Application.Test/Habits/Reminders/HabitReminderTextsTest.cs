using LifeManager.Application.Habits.Reminders;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.UsersPreferences.Enums;
using System.Text;
using System.Text.Json;

namespace LifeManager.Application.Test.Habits.Reminders
{
    public class HabitReminderTextsTest
    {
        private static readonly DateOnly Today = new(2026, 11, 11);
        private static readonly DateTimeOffset Now = new(2026, 11, 11, 15, 0, 0, TimeSpan.Zero);

        private static HabitReminder Reminder(int id, string name, int? remaining = null, string? unit = null)
            => new(Habit.FromPersistence(
                id, 1, name, null, null, HabitKind.Positive, HabitDifficulty.Easy, HabitFrequencyType.Daily, HabitWeekDays.None, null,
                Today, Now, null, 0, 0, Today.AddDays(-1)), remaining, unit);

        [Fact]
        public void For_ShouldNameTheHabit_WhenThereIsOne()
        {
            var payload = HabitReminderTexts.For([Reminder(1, "Ler")], Language.PTBR, Today, 20);

            Assert.Equal("LifeManager", payload.Title);
            Assert.Equal("Ler: ainda dá tempo hoje", payload.Body);
            Assert.Equal(HabitReminderTexts.Url, payload.Url);
            Assert.Equal("habits-2026-11-11-20", payload.Tag);
        }

        [Fact]
        public void For_ShouldTellWhatIsLeft_ForAHabitWithATarget()
        {
            var payload = HabitReminderTexts.For([Reminder(1, "Beber água", 1750, "ml")], Language.PTBR, Today, 9);

            Assert.Equal("Beber água: faltam 1.750 ml", payload.Body);
            Assert.Equal("habits-2026-11-11-09", payload.Tag);
        }

        [Fact]
        public void For_ShouldListEveryHabit_WhenThereAreSeveral()
        {
            var payload = HabitReminderTexts.For([Reminder(1, "Academia"), Reminder(2, "Beber água", 750, "ml"), Reminder(3, "Ler")], Language.PTBR, Today, 20);

            Assert.Equal("Faltam 3 hábitos: Academia, Beber água (750 ml), Ler", payload.Body);
        }

        [Fact]
        public void For_ShouldWriteInEnglish_WhenThatIsTheUsersLanguage()
        {
            Assert.Equal("Read: there's still time today", HabitReminderTexts.For([Reminder(1, "Read")], Language.EN, Today, 20).Body);
            Assert.Equal("Drink water: 1,750 ml to go", HabitReminderTexts.For([Reminder(1, "Drink water", 1750, "ml")], Language.EN, Today, 20).Body);
            Assert.Equal("2 habits to go: Gym, Read", HabitReminderTexts.For([Reminder(1, "Gym"), Reminder(2, "Read")], Language.EN, Today, 20).Body);
        }

        [Fact]
        public void For_ShouldCutTheList_SoThePayloadStaysSmall()
        {
            var reminders = Enumerable.Range(1, 60).Select(id => Reminder(id, new string((char)('a' + id % 26), 60))).ToList();

            var payload = HabitReminderTexts.For(reminders, Language.PTBR, Today, 20);

            Assert.StartsWith("Faltam 60 hábitos: ", payload.Body);
            Assert.Matches(@" \+\d+$", payload.Body);
            Assert.True(payload.Body.Length <= HabitReminderTexts.MaxBodyLength);
            Assert.True(Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(payload)) < 2048);
        }
    }
}
