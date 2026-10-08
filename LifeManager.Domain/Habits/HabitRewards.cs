using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Habits
{
    /// <summary>
    /// What completing a habit's day is worth: a check-in of a habit to build, or a clean day of a habit to avoid.
    /// </summary>
    public static class HabitRewards
    {
        /// <summary>A week of a times-per-week streak is worth this many days of streak bonus.</summary>
        public const int DaysPerStreakWeek = 7;

        /// <summary>
        /// The habit's coins (with the streak bonus, <paramref name="streak"/> already counting this day), XP and the
        /// completion's HP. Nothing once a times-per-week habit to build had already met its target that week.
        /// </summary>
        /// <param name="successDates">Already including <paramref name="date"/>.</param>
        public static GameDelta ForCompletion(Habit habit, DateOnly date, int streak, IReadOnlySet<DateOnly> successDates)
        {
            if (IsWeekly(habit) && StreakCalculator.CountInWeek(successDates, date) - 1 >= habit.TimesPerWeek)
                return GameDelta.None;

            var reward = GameRules.Reward(habit.Difficulty);
            var coins = GameRules.CoinsWithStreakBonus(reward.Coins, StreakBonusDays(habit, streak));

            return new GameDelta(coins, reward.Xp, GameRules.HealPerCompletion);
        }

        /// <summary>The streak in days for the coin bonus: a weekly habit's streak counts in weeks.</summary>
        public static int StreakBonusDays(Habit habit, int streak)
            => IsWeekly(habit) ? streak * DaysPerStreakWeek : streak;

        private static bool IsWeekly(Habit habit)
            => habit.FrequencyType == HabitFrequencyType.TimesPerWeek;
    }
}
