using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Habits.Services
{
    /// <summary>
    /// The day close: judges the days nobody can check in any more (before yesterday). A habit to build that was due and
    /// not done costs HP (or a streak freeze); a habit to avoid with no relapse earns a clean day; a weekly habit is
    /// judged when its week closes. The schedule is each habit's cursor (<c>EvaluatedUntil</c>), so days missed while the
    /// API was down are caught up, and running twice never judges a day twice.
    /// </summary>
    public class HabitEvaluationService(IHabitEvaluationRepository habitEvaluationRepository, AppClock appClock, TimeProvider timeProvider)
    {
        /// <summary>How many users one run picks up; the rest wait for the next run.</summary>
        public const int UserBatchSize = 500;

        private readonly IHabitEvaluationRepository _habitEvaluationRepository = habitEvaluationRepository;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<IReadOnlyList<UserId>> GetUserIdsToEvaluateAsync(CancellationToken cancellationToken)
            => await _habitEvaluationRepository.GetUserIdsToEvaluateAsync(LastClosedDay(_appClock.Today()), UserBatchSize, cancellationToken);

        /// <summary>
        /// Judges the user's closed days in date order, across all habits, so knockouts land in the order things
        /// happened. After <see cref="GameRules.MaxKnockoutsPerClose"/> knockouts, the rest of the run's damage is forgiven
        /// (the misses are still recorded and still break the streaks).
        /// </summary>
        public async Task<HabitEvaluationSummaryDto> EvaluateUserAsync(UserId userId, CancellationToken cancellationToken)
        {
            var today = _appClock.Today();
            var lastClosedDay = LastClosedDay(today);

            var habits = (await _habitEvaluationRepository.GetHabitsToEvaluateAsync(userId, lastClosedDay, cancellationToken)).ToList();
            if (habits.Count == 0)
                return new HabitEvaluationSummaryDto(0, 0);

            var knockouts = 0;
            var daysEvaluated = 0;
            var now = _timeProvider.GetUtcNow();

            for (var date = habits.Min(habit => habit.EvaluatedUntil).AddDays(1); date <= lastClosedDay && habits.Count > 0; date = date.AddDays(1))
            {
                foreach (var habit in habits.Where(habit => habit.EvaluatedUntil == date.AddDays(-1)).ToList())
                {
                    var forgiveDamage = knockouts >= GameRules.MaxKnockoutsPerClose;
                    var judgedDate = date;

                    var effects = await _habitEvaluationRepository.EvaluateDayAsync(
                        habit, judgedDate, context => Decide(habit, judgedDate, context, today, now, forgiveDamage), cancellationToken);

                    // Someone else moved its cursor, or it was archived meanwhile: leave it to them.
                    if (effects is null)
                    {
                        habits.Remove(habit);
                        continue;
                    }

                    daysEvaluated++;
                    if (effects.KnockedOut)
                        knockouts++;
                }
            }

            return new HabitEvaluationSummaryDto(daysEvaluated, knockouts);
        }

        /// <summary>The last day nobody can check in any more: the day before yesterday.</summary>
        public static DateOnly LastClosedDay(DateOnly today)
            => today.AddDays(-StreakCalculator.EditableDays);

        private static HabitEvaluationEffects Decide(
            Habit habit,
            DateOnly date,
            HabitEvaluationContext context,
            DateOnly today,
            DateTimeOffset now,
            bool forgiveDamage)
        {
            var verdict = HabitDayVerdict.Judge(habit, date, context.WeekCheckIns);

            return verdict.Kind switch
            {
                HabitDayVerdictKind.Missed => Missed(habit, date, context, today, now, forgiveDamage, [date]),
                HabitDayVerdictKind.WeekShort => Missed(habit, date, context, today, now, forgiveDamage, FreeDaysOfWeek(context, date, verdict.Shortfall)),
                HabitDayVerdictKind.Clean => Clean(habit, date, context, today, now),
                _ => HabitEvaluationEffects.None
            };
        }

        /// <summary>
        /// A missed day, or a week short of its target by <c>missedDays.Count</c>: a streak freeze, if the player has one,
        /// protects it all (no damage, and frozen days that keep the streak); otherwise each missed day costs HP. A daily
        /// miss is recorded as a missed check-in; a short week only in the ledger.
        /// </summary>
        private static HabitEvaluationEffects Missed(
            Habit habit,
            DateOnly date,
            HabitEvaluationContext context,
            DateOnly today,
            DateTimeOffset now,
            bool forgiveDamage,
            IReadOnlyList<DateOnly> missedDays)
        {
            var weekly = habit.FrequencyType == HabitFrequencyType.TimesPerWeek;
            var profile = context.Profile;

            if (profile.UseStreakFreeze().IsSuccess)
            {
                var frozenOutcome = profile.Apply(GameDelta.None);
                var frozen = missedDays.Select(day => HabitCheckIn.Judged(habit, day, HabitCheckInStatus.Frozen, now, GameDelta.None)).ToList();
                var successDates = new HashSet<DateOnly>(context.SuccessDates);
                successDates.UnionWith(missedDays);

                return new HabitEvaluationEffects(
                    frozen,
                    GameLedgerEntry.FromOutcome(habit.UserId, GameLedgerEntryKind.FreezeUsed, date, now, habit.Name.Value, frozenOutcome, habit.Id),
                    StreakCalculator.Current(habit.Frequency, habit.StartDate, successDates, today),
                    KnockedOut: false);
            }

            var damage = forgiveDamage ? 0 : GameRules.Damage(habit.Difficulty) * missedDays.Count;
            var outcome = profile.Apply(new GameDelta(0, 0, -damage));
            IReadOnlyList<HabitCheckIn> missed = weekly ? [] : [HabitCheckIn.Judged(habit, date, HabitCheckInStatus.Missed, now, outcome.Applied)];

            return new HabitEvaluationEffects(
                missed,
                GameLedgerEntry.FromOutcome(habit.UserId, GameLedgerEntryKind.HabitMissed, date, now, habit.Name.Value, outcome, habit.Id),
                StreakCalculator.Current(habit.Frequency, habit.StartDate, context.SuccessDates, today),
                outcome.KnockedOut);
        }

        /// <summary>A habit to avoid kept on a due day: rewarded like a check-in, milestones and freezes included.</summary>
        private static HabitEvaluationEffects Clean(Habit habit, DateOnly date, HabitEvaluationContext context, DateOnly today, DateTimeOffset now)
        {
            var streakBefore = StreakCalculator.Current(habit.Frequency, habit.StartDate, context.SuccessDates, today);
            var successDates = new HashSet<DateOnly>(context.SuccessDates) { date };
            var streak = StreakCalculator.Current(habit.Frequency, habit.StartDate, successDates, today);
            var completion = HabitRewards.ApplyCompletion(habit, context.Profile, GameLedgerEntryKind.CleanDay, date, now, streakBefore, streak, successDates);

            return new HabitEvaluationEffects(
                [HabitCheckIn.Judged(habit, date, HabitCheckInStatus.Clean, now, completion.Applied, completion.FreezesEarned > 0)],
                completion.Entries,
                streak,
                completion.Outcome.KnockedOut);
        }

        /// <summary>The first <paramref name="count"/> days of <paramref name="date"/>'s week that have no check-in.</summary>
        private static List<DateOnly> FreeDaysOfWeek(HabitEvaluationContext context, DateOnly date, int count)
        {
            var weekStart = StreakCalculator.WeekStart(date);
            var taken = context.WeekCheckIns.Select(checkIn => checkIn.Date).ToHashSet();

            return [.. Enumerable.Range(0, 7).Select(weekStart.AddDays).Where(day => !taken.Contains(day)).Take(count)];
        }
    }
}
