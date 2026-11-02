using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Ranking;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Habits.Challenges
{
    /// <summary>Pure rules of a weekly challenge: which week, how high a target, how hard it is and what it pays.</summary>
    public static class ChallengeRules
    {
        public static DateOnly WeekStartFor(ChallengeWeek week, DateOnly today)
            => week == ChallengeWeek.Next ? GameWeek.Start(today).AddDays(GameWeek.Days) : GameWeek.Start(today);

        /// <summary>The current week until Wednesday, the next one any time.</summary>
        public static Result EnsureWeekIsOpen(DateOnly weekStart, DateOnly today)
        {
            if (!GameWeek.IsWeekStart(weekStart))
                return ChallengeErrors.InvalidWeek;

            var currentWeek = GameWeek.Start(today);
            if (weekStart == currentWeek.AddDays(GameWeek.Days))
                return Result.Success();

            if (weekStart == currentWeek && DaysIntoWeek(today) <= DaysIntoWeek(GameRules.ChallengeLastCreationDay))
                return Result.Success();

            return ChallengeErrors.CreationWindowClosed;
        }

        /// <summary>Days the habit can be done in the week: the scheduled ones from its start date on.</summary>
        public static int PossibleDays(Habit habit, DateOnly weekStart)
            => WeekDays(weekStart).Count(day => day >= habit.StartDate && habit.Frequency.IsScheduledOn(day));

        /// <summary>
        /// The most days a frequency challenge can ask for: the possible days, and in a week already under way only the
        /// ones done or still open to a check-in (yesterday on).
        /// </summary>
        public static int MaxFrequencyTarget(Habit habit, DateOnly weekStart, DateOnly today, IReadOnlyCollection<DateOnly> doneDates)
            => WeekDays(weekStart).Count(day =>
                day >= habit.StartDate
                && habit.Frequency.IsScheduledOn(day)
                && (doneDates.Contains(day) || day >= today.AddDays(-1)));

        public static int MaxVolumeTarget(Habit habit)
            => GameWeek.Days * (habit.TargetValue ?? 0) * GameRules.ChallengeMaxVolumeWeeks;

        /// <summary>
        /// The Mondays of the weeks the baseline averages: up to four ended weeks, newest first, whole ones only (a habit
        /// started midweek leaves its first week out, which would pull the average down).
        /// </summary>
        public static IReadOnlyList<DateOnly> ClosedWeeks(DateOnly today, DateOnly habitStart)
        {
            var currentWeek = GameWeek.Start(today);
            var firstWeek = GameWeek.IsWeekStart(habitStart) ? habitStart : GameWeek.Start(habitStart).AddDays(GameWeek.Days);

            return [.. Enumerable.Range(1, GameRules.ChallengeBaselineWeeks)
                .Select(weeksBack => currentWeek.AddDays(-GameWeek.Days * weeksBack))
                .Where(weekStart => weekStart >= firstWeek)];
        }

        /// <summary>The weekly average, to two places; null without any closed week.</summary>
        public static decimal? Baseline(IReadOnlyCollection<int> weekTotals)
            => weekTotals.Count == 0 ? null : Math.Round((decimal)weekTotals.Sum() / weekTotals.Count, 2);

        /// <summary>
        /// How hard the target is against the baseline. Without one there is nothing to beat, so it is light: a brand-new
        /// habit can't take the bigger rewards (or the ranking points) for a target of one day.
        /// </summary>
        public static ChallengeEffort Effort(ChallengeType type, int target, decimal? baseline)
        {
            if (baseline is null || target <= baseline)
                return ChallengeEffort.Light;

            var steadyLimit = type == ChallengeType.Frequency ? baseline.Value + 1 : baseline.Value * GameRules.ChallengeSteadyFactor;

            return target <= steadyLimit ? ChallengeEffort.Steady : ChallengeEffort.Stretch;
        }

        public static ChallengeReward Reward(HabitDifficulty difficulty, ChallengeEffort effort)
        {
            var reward = GameRules.Reward(difficulty);
            var multiplier = GameRules.ChallengeRewardMultiplier(effort);

            return new ChallengeReward(reward.Coins * multiplier, reward.Xp * multiplier);
        }

        /// <summary>
        /// One challenge per habit with a closed week: a bit above its average (one more day, or 15% more of a measured
        /// habit's amount, which is preferred), the most forgotten habit first, then the hardest, up to the week's room.
        /// </summary>
        public static IReadOnlyList<ChallengeSuggestion> Suggest(IEnumerable<ChallengeCandidate> candidates, int existingChallenges)
        {
            var room = Math.Max(0, GameRules.MaxChallengesPerWeek - existingChallenges);

            return [.. candidates
                .Select(candidate => (Candidate: candidate, Suggestion: SuggestFor(candidate)))
                .Where(pair => pair.Suggestion is not null)
                .OrderBy(pair => Consistency(pair.Candidate, pair.Suggestion!.Type))
                .ThenByDescending(pair => pair.Candidate.Habit.Difficulty)
                .ThenBy(pair => pair.Candidate.Habit.Id!.Value)
                .Select(pair => pair.Suggestion!)
                .Take(room)];
        }

        private static ChallengeSuggestion? SuggestFor(ChallengeCandidate candidate)
        {
            var habit = candidate.Habit;

            if (habit.Target is { } dailyTarget && candidate.VolumeBaseline is { } volumeBaseline)
            {
                var step = dailyTarget.Step ?? 1;
                var raised = (int)Math.Ceiling(volumeBaseline * GameRules.ChallengeVolumeSuggestionFactor / step) * step;
                var target = Math.Min(Math.Max(raised, step), MaxVolumeTarget(habit));

                return target <= candidate.VolumeProgress ? null : Suggestion(habit, ChallengeType.Volume, target, volumeBaseline);
            }

            if (candidate.FrequencyBaseline is { } frequencyBaseline && frequencyBaseline < candidate.MaxFrequency)
            {
                var target = Math.Min((int)Math.Ceiling(frequencyBaseline) + 1, candidate.MaxFrequency);

                return target <= candidate.FrequencyProgress ? null : Suggestion(habit, ChallengeType.Frequency, target, frequencyBaseline);
            }

            return null;
        }

        private static ChallengeSuggestion Suggestion(Habit habit, ChallengeType type, int target, decimal baseline)
        {
            var effort = Effort(type, target, baseline);

            return new ChallengeSuggestion(habit.Id!, type, target, baseline, effort, Reward(habit.Difficulty, effort));
        }

        /// <summary>How close the habit's usual week is to a full one (0 to 1): the lower, the more it needs a push.</summary>
        private static decimal Consistency(ChallengeCandidate candidate, ChallengeType type)
        {
            if (type == ChallengeType.Volume)
                return candidate.VolumeBaseline!.Value / Math.Max(1, candidate.PossibleDays * candidate.Habit.TargetValue!.Value);

            var expectedDays = candidate.Habit.TimesPerWeek ?? candidate.PossibleDays;

            return Math.Min(1, candidate.FrequencyBaseline!.Value / Math.Max(1, expectedDays));
        }

        private static IEnumerable<DateOnly> WeekDays(DateOnly weekStart)
            => Enumerable.Range(0, GameWeek.Days).Select(weekStart.AddDays);

        private static int DaysIntoWeek(DateOnly date)
            => date.DayNumber - GameWeek.Start(date).DayNumber;

        private static int DaysIntoWeek(DayOfWeek day)
            => ((int)day + 6) % 7;
    }
}
