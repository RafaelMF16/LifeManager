using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits
{
    /// <summary>What a streak passed on its way up: milestone coins and streak freezes.</summary>
    /// <param name="MilestoneDays">The biggest milestone passed (in days), for the message; null when none.</param>
    public record StreakGain(int MilestoneCoins, int FreezesEarned, int? MilestoneDays)
    {
        public static readonly StreakGain None = new(0, 0, null);
    }

    /// <summary>What <see cref="HabitRewards.ApplyCompletion"/> did to the profile.</summary>
    /// <param name="Outcome">The reward and the milestone, combined (<see cref="GameOutcome.Combine"/>).</param>
    /// <param name="FreezesEarned">Freezes actually added (the profile holds at most <see cref="GameRules.MaxStreakFreezes"/>).</param>
    public record HabitCompletion(GameOutcome Outcome, IReadOnlyList<GameLedgerEntry> Entries, int? MilestoneDays, int MilestoneCoins, int FreezesEarned)
    {
        /// <summary>Everything the completion added, so undoing it gives back exactly that.</summary>
        public GameDelta Applied => Outcome.Applied;
    }

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

        /// <summary>
        /// The milestones and freezes the streak passed going from <paramref name="streakBefore"/> to
        /// <paramref name="streakAfter"/>, counted in days (a weekly habit's week is worth 7).
        /// </summary>
        public static StreakGain StreakGain(Habit habit, int streakBefore, int streakAfter)
        {
            var before = StreakBonusDays(habit, streakBefore);
            var after = StreakBonusDays(habit, streakAfter);
            var milestones = GameRules.MilestonesCrossed(before, after);

            return new StreakGain(
                milestones.Sum(GameRules.MilestoneBonus),
                GameRules.FreezesEarnedCrossed(before, after),
                milestones.Count > 0 ? milestones[^1] : null);
        }

        /// <summary>
        /// Applies a completed day to the locked <paramref name="profile"/>: the reward (a <paramref name="kind"/> ledger
        /// entry), then the coins of any milestone the streak passed (a <see cref="GameLedgerEntryKind.StreakMilestone"/>
        /// entry) and the freezes it earned.
        /// </summary>
        /// <param name="successDates">Already including <paramref name="date"/>.</param>
        public static HabitCompletion ApplyCompletion(
            Habit habit,
            PlayerProfile profile,
            GameLedgerEntryKind kind,
            DateOnly date,
            DateTimeOffset now,
            int streakBefore,
            int streakAfter,
            IReadOnlySet<DateOnly> successDates)
        {
            var userId = habit.UserId;
            var rewardOutcome = profile.Apply(ForCompletion(habit, date, streakAfter, successDates));
            var entries = new List<GameLedgerEntry>(GameLedgerEntry.FromOutcome(userId, kind, date, now, habit.Name.Value, rewardOutcome, habit.Id));
            var outcomes = new List<GameOutcome> { rewardOutcome };

            var gain = StreakGain(habit, streakBefore, streakAfter);

            if (gain.MilestoneCoins > 0)
            {
                var milestoneOutcome = profile.Apply(new GameDelta(gain.MilestoneCoins, 0, 0));
                entries.AddRange(GameLedgerEntry.FromOutcome(userId, GameLedgerEntryKind.StreakMilestone, date, now, habit.Name.Value, milestoneOutcome, habit.Id));
                outcomes.Add(milestoneOutcome);
            }

            var freezesEarned = 0;
            for (var i = 0; i < gain.FreezesEarned && profile.AddStreakFreeze().IsSuccess; i++)
                freezesEarned++;

            return new HabitCompletion(GameOutcome.Combine(outcomes), entries, gain.MilestoneDays, gain.MilestoneCoins, freezesEarned);
        }

        /// <summary>The streak in days for the coin bonus: a weekly habit's streak counts in weeks.</summary>
        public static int StreakBonusDays(Habit habit, int streak)
            => IsWeekly(habit) ? streak * DaysPerStreakWeek : streak;

        private static bool IsWeekly(Habit habit)
            => habit.FrequencyType == HabitFrequencyType.TimesPerWeek;
    }
}
