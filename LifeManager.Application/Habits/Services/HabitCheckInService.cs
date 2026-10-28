using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Habits.Services
{
    /// <summary>
    /// The day's checklist: checking a habit in earns its coins (with the streak bonus), XP and a little HP; undoing
    /// gives back exactly what it earned. Only today and yesterday can change; older days are the day close's job.
    /// </summary>
    public class HabitCheckInService(
        IHabitRepository habitRepository,
        IHabitCheckInRepository habitCheckInRepository,
        AppClock appClock,
        TimeProvider timeProvider)
    {
        /// <summary>How far back the day's checklist looks for missed days, to welcome the player back after one.</summary>
        public const int RecentMissDays = 7;

        private readonly IHabitRepository _habitRepository = habitRepository;
        private readonly IHabitCheckInRepository _habitCheckInRepository = habitCheckInRepository;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<Result<HabitTodayDto>> GetTodayAsync(UserId userId, CancellationToken cancellationToken)
        {
            var today = _appClock.Today();
            var yesterday = today.AddDays(-1);

            var habits = await _habitRepository.GetActiveByUserIdAsync(userId, HabitKind.Positive, cancellationToken);
            var habitsToAvoid = await _habitRepository.GetActiveByUserIdAsync(userId, HabitKind.Negative, cancellationToken);
            // Covers yesterday's and today's week (they may differ on a Monday) and the recent misses.
            var weekStart = StreakCalculator.WeekStart(yesterday);
            var recentStart = today.AddDays(-RecentMissDays);
            var from = recentStart < weekStart ? recentStart : weekStart;
            var checkIns = await _habitCheckInRepository.GetByUserIdAsync(userId, from, today, cancellationToken);

            var activeHabitIds = habits.Concat(habitsToAvoid).Select(habit => habit.Id!.Value).ToHashSet();
            var recentMisses = checkIns
                .Where(checkIn => checkIn.Status is HabitCheckInStatus.Missed or HabitCheckInStatus.Relapse
                    && checkIn.Date >= recentStart
                    && activeHabitIds.Contains(checkIn.HabitId.Value))
                .ToList();
            DateOnly? lastMissedOn = recentMisses.Count > 0 ? recentMisses.Max(checkIn => checkIn.Date) : null;

            var successDatesByHabit = checkIns
                .Where(checkIn => checkIn.IsSuccess)
                .GroupBy(checkIn => checkIn.HabitId.Value)
                .ToDictionary(group => group.Key, group => group.Select(checkIn => checkIn.Date).ToHashSet());

            var todayItems = new List<HabitTodayItemDto>();
            var yesterdayPending = new List<HabitTodayItemDto>();

            foreach (var habit in habits)
            {
                var successDates = successDatesByHabit.GetValueOrDefault(habit.Id!.Value) ?? [];

                if (habit.EnsureCanCheckIn(today, today).IsSuccess)
                    todayItems.Add(ToTodayItem(habit, today, successDates));

                if (habit.EnsureCanCheckIn(yesterday, today).IsSuccess
                    && !successDates.Contains(yesterday)
                    && !IsWeekTargetMet(habit, successDates, yesterday))
                    yesterdayPending.Add(ToTodayItem(habit, yesterday, successDates));
            }

            var relapseDatesByHabit = checkIns
                .Where(checkIn => checkIn.Status == HabitCheckInStatus.Relapse)
                .GroupBy(checkIn => checkIn.HabitId.Value)
                .ToDictionary(group => group.Key, group => group.Select(checkIn => checkIn.Date).ToHashSet());

            var avoiding = new List<HabitAvoidItemDto>();
            var freeToday = new List<string>();

            foreach (var habit in habitsToAvoid)
            {
                if (habit.EnsureCanRelapse(today, today).IsSuccess)
                    avoiding.Add(ToAvoidItem(habit, today, relapseDatesByHabit.GetValueOrDefault(habit.Id!.Value) ?? []));
                else if (habit.StartDate <= today && !habit.Frequency.IsScheduledOn(today))
                    freeToday.Add(habit.Name.Value);
            }

            return new HabitTodayDto(today, todayItems, yesterdayPending, lastMissedOn, recentMisses.Count, avoiding, freeToday);
        }

        private static HabitAvoidItemDto ToAvoidItem(Habit habit, DateOnly today, IReadOnlySet<DateOnly> relapseDates)
        {
            var yesterday = today.AddDays(-1);
            var relapsedToday = relapseDates.Contains(today);
            var relapsedYesterday = relapseDates.Contains(yesterday);
            int? weekRelapseCount = habit.FrequencyType == HabitFrequencyType.TimesPerWeek ? StreakCalculator.CountInWeek(relapseDates, today) : null;

            return new HabitAvoidItemDto(
                habit.Id!.Value,
                habit.Name.Value,
                habit.Trigger?.Value,
                habit.Difficulty,
                habit.FrequencyType,
                habit.TimesPerWeek,
                habit.CurrentStreak,
                habit.LongestStreak,
                relapsedToday,
                relapsedYesterday,
                !relapsedYesterday && habit.EnsureCanRelapse(yesterday, today).IsSuccess,
                weekRelapseCount,
                relapsedToday ? 0 : HabitRewards.RelapseDamage(habit, (weekRelapseCount ?? 0) + 1));
        }

        public async Task<Result<HabitCheckInResultDto>> CheckInAsync(int id, HabitCheckInDto checkInDto, UserId userId, CancellationToken cancellationToken)
        {
            var habit = await _habitRepository.GetByIdAsync(new HabitId(id), userId, cancellationToken);
            if (habit is null)
                return HabitErrors.NotFound;

            var date = checkInDto.Date;
            var today = _appClock.Today();

            var canCheckIn = habit.EnsureCanCheckIn(date, today);
            if (!canCheckIn.IsSuccess)
                return canCheckIn.Error;

            var now = _timeProvider.GetUtcNow();
            PlayerProfile? profile = null;

            HabitCompletion? completion = null;

            var effects = await _habitCheckInRepository.RecordAsync(habit, date, HabitCheckInStatus.Done, now, context =>
            {
                profile = context.Profile;
                (var dayEffects, completion) = CompleteDay(habit, context.Profile, date, now, context.SuccessDates, context.FailedDates, today);

                return dayEffects;
            }, cancellationToken);

            if (effects is null)
                return HabitErrors.AlreadyCheckedIn;

            return new HabitCheckInResultDto(
                habit.Id!.Value,
                date,
                true,
                habit.CurrentStreak,
                habit.LongestStreak,
                WalletChangeDto.From(profile!, effects.Outcome),
                completion!.MilestoneDays,
                completion.MilestoneCoins,
                completion.FreezesEarned);
        }

        /// <summary>
        /// Applies a completed day to the locked profile: the reward with its streak bonus, milestone and freeze. Shared by
        /// a check-in and by progress reaching the day's target.
        /// </summary>
        /// <param name="successDates">The habit's success days, <paramref name="date"/> included.</param>
        internal static (HabitCheckInEffects Effects, HabitCompletion Completion) CompleteDay(
            Habit habit,
            PlayerProfile profile,
            DateOnly date,
            DateTimeOffset now,
            IReadOnlySet<DateOnly> successDates,
            IReadOnlySet<DateOnly> failedDates,
            DateOnly today)
        {
            var streakBefore = StreakCalculator.Current(habit, Without(successDates, date), failedDates, today);
            var streak = StreakCalculator.Current(habit, successDates, failedDates, today);
            var completion = HabitRewards.ApplyCompletion(habit, profile, GameLedgerEntryKind.HabitDone, date, now, streakBefore, streak, successDates);

            return (new HabitCheckInEffects(streak, completion.Applied, completion.Outcome, completion.Entries, completion.FreezesEarned > 0), completion);
        }

        /// <summary>
        /// Gives back what <paramref name="removed"/> earned to the locked profile, milestone coins and the streak freeze it
        /// earned included (if the player still holds one). The HP it healed is taken back only down to 1: an undo never
        /// knocks the player out. Shared by an undone check-in and by progress falling below the day's target.
        /// </summary>
        /// <param name="successDates">The habit's success days, without <paramref name="removed"/>'s.</param>
        internal static HabitCheckInEffects UndoDay(
            Habit habit,
            PlayerProfile profile,
            HabitCheckIn removed,
            DateTimeOffset now,
            IReadOnlySet<DateOnly> successDates,
            IReadOnlySet<DateOnly> failedDates,
            DateOnly today)
        {
            var streak = StreakCalculator.Current(habit, successDates, failedDates, today);

            var awarded = removed.Awarded;
            var hpToTakeBack = Math.Min(awarded.Hp, Math.Max(profile.Hp - 1, 0));
            var outcome = profile.Apply(new GameDelta(-awarded.Coins, -awarded.Xp, -hpToTakeBack));
            var entries = GameLedgerEntry.FromOutcome(habit.UserId, GameLedgerEntryKind.Undo, removed.Date, now, habit.Name.Value, outcome, habit.Id);

            // A freeze the player already spent is gone: nothing to take back.
            if (removed.FreezeAwarded)
                profile.UseStreakFreeze();

            return new HabitCheckInEffects(streak, outcome.Applied, outcome, entries);
        }

        internal static HashSet<DateOnly> Without(IReadOnlySet<DateOnly> dates, DateOnly date)
            => [.. dates.Where(day => day != date)];

        /// <summary>Gives back what the check-in earned (<see cref="UndoDay"/>).</summary>
        public async Task<Result<HabitCheckInResultDto>> UndoCheckInAsync(int id, DateOnly date, UserId userId, CancellationToken cancellationToken)
        {
            var habit = await _habitRepository.GetByIdAsync(new HabitId(id), userId, cancellationToken);
            if (habit is null)
                return HabitErrors.NotFound;

            var today = _appClock.Today();

            var canUndo = habit.EnsureCanCheckIn(date, today);
            if (!canUndo.IsSuccess)
                return canUndo.Error;

            var now = _timeProvider.GetUtcNow();
            PlayerProfile? profile = null;

            var effects = await _habitCheckInRepository.RemoveAsync(habit, date, HabitCheckInStatus.Done, context =>
            {
                profile = context.Profile;

                return UndoDay(habit, context.Profile, context.Removed!, now, context.SuccessDates, context.FailedDates, today);
            }, cancellationToken);

            if (effects is null)
                return HabitErrors.CheckInNotFound;

            return new HabitCheckInResultDto(
                habit.Id!.Value, date, false, habit.CurrentStreak, habit.LongestStreak, WalletChangeDto.From(profile!, effects.Outcome), null, 0, 0);
        }

        private static bool IsWeekTargetMet(Habit habit, IReadOnlySet<DateOnly> successDates, DateOnly date)
            => habit.FrequencyType == HabitFrequencyType.TimesPerWeek
                && StreakCalculator.CountInWeek(successDates, date) >= habit.TimesPerWeek;

        private static HabitTodayItemDto ToTodayItem(Habit habit, DateOnly date, IReadOnlySet<DateOnly> successDates)
        {
            var done = successDates.Contains(date);
            var weekly = habit.FrequencyType == HabitFrequencyType.TimesPerWeek;
            int? weekDoneCount = weekly ? StreakCalculator.CountInWeek(successDates, date) : null;

            // An estimate from the stored streak: a daily habit's next check-in usually extends it by one.
            var coinsPreview = !done && IsWeekTargetMet(habit, successDates, date)
                ? 0
                : GameRules.CoinsWithStreakBonus(
                    GameRules.Reward(habit.Difficulty).Coins,
                    HabitRewards.StreakBonusDays(habit, habit.CurrentStreak + (done || weekly ? 0 : 1)));

            return new HabitTodayItemDto(
                habit.Id!.Value,
                habit.Name.Value,
                habit.Trigger?.Value,
                habit.Difficulty,
                habit.FrequencyType,
                habit.TimesPerWeek,
                habit.CurrentStreak,
                habit.LongestStreak,
                done,
                weekDoneCount,
                coinsPreview);
        }
    }
}
