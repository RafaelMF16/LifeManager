using LifeManager.Domain.Habits;
using LifeManager.Domain.Push.Interfaces;
using LifeManager.Domain.UsersPreferences.Enums;
using System.Globalization;
using System.Text;

namespace LifeManager.Application.Habits.Reminders
{
    /// <summary>
    /// The reminder notification in the user's language: one habit is named ("Ler: ainda dá tempo hoje", or what is left
    /// of its target), several are listed ("Faltam 2 hábitos: Ler, Academia"), cut with "+N" so the payload stays far
    /// below the push services' 4 KB.
    /// </summary>
    public static class HabitReminderTexts
    {
        public const string Title = "LifeManager";

        /// <summary>The frontend's Today screen, which a tap on the notification opens.</summary>
        public const string Url = "/habits/today";

        public const int MaxBodyLength = 240;

        private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");
        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

        /// <param name="reminders">At least one, in the order to show.</param>
        public static PushPayload For(IReadOnlyList<HabitReminder> reminders, Language language, DateOnly date, int hour)
        {
            var english = language == Language.EN;
            var body = reminders.Count == 1 ? Single(reminders[0], english) : List(reminders, english);

            return new PushPayload(Title, body, Url, $"habits-{date:yyyy-MM-dd}-{hour:00}");
        }

        private static string Single(HabitReminder reminder, bool english)
        {
            var name = reminder.Habit.Name.Value;

            if (reminder.Remaining is { } remaining)
                return english
                    ? $"{name}: {Amount(remaining, reminder.Unit, English)} to go"
                    : $"{name}: faltam {Amount(remaining, reminder.Unit, Portuguese)}";

            return english ? $"{name}: there's still time today" : $"{name}: ainda dá tempo hoje";
        }

        private static string List(IReadOnlyList<HabitReminder> reminders, bool english)
        {
            var body = new StringBuilder(english ? $"{reminders.Count} habits to go: " : $"Faltam {reminders.Count} hábitos: ");
            var culture = english ? English : Portuguese;
            var listed = 0;

            foreach (var reminder in reminders)
            {
                var item = reminder.Remaining is { } remaining
                    ? $"{reminder.Habit.Name.Value} ({Amount(remaining, reminder.Unit, culture)})"
                    : reminder.Habit.Name.Value;
                var separator = listed == 0 ? "" : ", ";
                var left = reminders.Count - listed - 1;

                // Room for this item and, if more come after it, the " +N" that would close the list.
                var suffixRoom = left == 0 ? 0 : $" +{left}".Length;
                if (body.Length + separator.Length + item.Length + suffixRoom > MaxBodyLength)
                    break;

                body.Append(separator).Append(item);
                listed++;
            }

            if (listed < reminders.Count)
                body.Append($" +{reminders.Count - listed}");

            return body.ToString();
        }

        private static string Amount(int value, string? unit, CultureInfo culture)
            => unit is null ? value.ToString("N0", culture) : $"{value.ToString("N0", culture)} {unit}";
    }
}
