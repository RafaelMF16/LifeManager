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
        private readonly IHabitRepository _habitRepository = habitRepository;
        private readonly IHabitCheckInRepository _habitCheckInRepository = habitCheckInRepository;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<Result<HabitTodayDto>> GetTodayAsync(UserId userId, CancellationToken cancellationToken)
        {
            var today = _appClock.Today();
            var yesterday = today.AddDays(-1);

            var habits = await _habitRepository.GetActiveByUserIdAsync(userId, HabitKind.Positive, cancellationToken);
            // From yesterday's week start: covers both yesterday's and today's week (they may differ on a Monday).
            var checkIns = await _habitCheckInRepository.GetByUserIdAsync(userId, StreakCalculator.WeekStart(yesterday), today, cancellationToken);

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

            return new HabitTodayDto(today, todayItems, yesterdayPending);
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

            var effects = await _habitCheckInRepository.CheckInAsync(habit, date, now, context =>
            {
                profile = context.Profile;
                var streak = StreakCalculator.Current(habit.Frequency, habit.StartDate, context.SuccessDates, today);
                var outcome = context.Profile.Apply(HabitRewards.ForCompletion(habit, date, streak, context.SuccessDates));
                var entries = GameLedgerEntry.FromOutcome(habit.UserId, GameLedgerEntryKind.HabitDone, date, now, habit.Name.Value, outcome, habit.Id);

                return new HabitCheckInEffects(streak, outcome.Applied, outcome, entries);
            }, cancellationToken);

            if (effects is null)
                return HabitErrors.AlreadyCheckedIn;

            return new HabitCheckInResultDto(habit.Id!.Value, date, true, habit.CurrentStreak, habit.LongestStreak, WalletChangeDto.From(profile!, effects.Outcome));
        }

        /// <summary>
        /// Gives back what the check-in earned. The HP it healed is taken back only down to 1: an undo never knocks the
        /// player out.
        /// </summary>
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

            var effects = await _habitCheckInRepository.UndoCheckInAsync(habit, date, context =>
            {
                profile = context.Profile;
                var streak = StreakCalculator.Current(habit.Frequency, habit.StartDate, context.SuccessDates, today);

                var awarded = context.Removed!.Awarded;
                var hpToTakeBack = Math.Min(awarded.Hp, Math.Max(context.Profile.Hp - 1, 0));
                var outcome = context.Profile.Apply(new GameDelta(-awarded.Coins, -awarded.Xp, -hpToTakeBack));
                var entries = GameLedgerEntry.FromOutcome(habit.UserId, GameLedgerEntryKind.Undo, date, now, habit.Name.Value, outcome, habit.Id);

                return new HabitCheckInEffects(streak, outcome.Applied, outcome, entries);
            }, cancellationToken);

            if (effects is null)
                return HabitErrors.CheckInNotFound;

            return new HabitCheckInResultDto(habit.Id!.Value, date, false, habit.CurrentStreak, habit.LongestStreak, WalletChangeDto.From(profile!, effects.Outcome));
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
                done,
                weekDoneCount,
                coinsPreview);
        }
    }
}
