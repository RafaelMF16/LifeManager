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
    /// Progress towards a habit's daily target: logging up to the target writes the day's done check-in with the same
    /// reward as a check-in, and removing below it undoes that check-in, giving back what it earned. Only today and
    /// yesterday can change, like check-ins.
    /// </summary>
    public class HabitProgressService(
        IHabitRepository habitRepository,
        IHabitProgressRepository habitProgressRepository,
        AppClock appClock,
        TimeProvider timeProvider)
    {
        private readonly IHabitRepository _habitRepository = habitRepository;
        private readonly IHabitProgressRepository _habitProgressRepository = habitProgressRepository;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<Result<HabitProgressResultDto>> AddAsync(int id, HabitProgressDto progressDto, UserId userId, CancellationToken cancellationToken)
        {
            var habit = await _habitRepository.GetByIdAsync(new HabitId(id), userId, cancellationToken);
            if (habit is null)
                return HabitErrors.NotFound;

            var date = progressDto.Date;
            var today = _appClock.Today();

            var canRecord = habit.EnsureCanRecordProgress(date, today);
            if (!canRecord.IsSuccess)
                return canRecord.Error;

            var now = _timeProvider.GetUtcNow();

            var entryResult = HabitProgressEntry.Create(habit, date, progressDto.Value, now);
            if (!entryResult.IsSuccess)
                return entryResult.Error;

            var entry = entryResult.Value;
            var target = habit.Target!;
            var change = new DayChange();

            var saved = await _habitProgressRepository.AddAsync(habit, entry, context =>
            {
                var canAdd = HabitProgress.EnsureCanAdd(context.DayEntries.Count);
                if (!canAdd.IsSuccess)
                    return canAdd.Error;

                change.Profile = context.Profile;
                // Any check-in already settles the day; only a done one can be undone later.
                change.Done = context.DayCheckIn is not null;

                var total = context.DayEntries.Sum(dayEntry => dayEntry.Value) + entry.Value;
                if (HabitProgress.ForAdded(target, total, change.Done) != HabitProgressDecision.Complete)
                    return HabitProgressEffects.None;

                var successDates = new HashSet<DateOnly>(context.SuccessDates) { date };
                (var effects, change.Completion) = HabitCheckInService.CompleteDay(habit, context.Profile, date, now, successDates, context.FailedDates, today);
                change.Done = true;

                return new HabitProgressEffects(HabitProgressDecision.Complete, effects);
            }, cancellationToken);

            if (!saved.IsSuccess)
                return saved.Error;

            return ToResultDto(habit, date, saved.Value, change);
        }

        public async Task<Result<HabitProgressResultDto>> RemoveAsync(int id, int entryId, UserId userId, CancellationToken cancellationToken)
        {
            var habit = await _habitRepository.GetByIdAsync(new HabitId(id), userId, cancellationToken);
            if (habit is null)
                return HabitErrors.NotFound;

            var today = _appClock.Today();
            var now = _timeProvider.GetUtcNow();
            var change = new DayChange();
            var date = today;

            var saved = await _habitProgressRepository.RemoveAsync(habit, new HabitProgressEntryId(entryId), context =>
            {
                date = context.Entry.Date;

                var canRecord = habit.EnsureCanRecordProgress(date, today);
                if (!canRecord.IsSuccess)
                    return canRecord.Error;

                change.Profile = context.Profile;
                var doneCheckIn = context.DayCheckIn?.Status == HabitCheckInStatus.Done ? context.DayCheckIn : null;
                change.Done = context.DayCheckIn is not null;

                var total = context.DayEntries.Where(dayEntry => dayEntry.Id != context.Entry.Id).Sum(dayEntry => dayEntry.Value);
                if (HabitProgress.ForRemoved(habit.Target!, total, doneCheckIn is not null) != HabitProgressDecision.Undo)
                    return HabitProgressEffects.None;

                var successDates = HabitCheckInService.Without(context.SuccessDates, date);
                var effects = HabitCheckInService.UndoDay(habit, context.Profile, doneCheckIn!, now, successDates, context.FailedDates, today);
                change.Done = false;

                return new HabitProgressEffects(HabitProgressDecision.Undo, effects);
            }, cancellationToken);

            if (!saved.IsSuccess)
                return saved.Error;

            return ToResultDto(habit, date, saved.Value, change);
        }

        /// <summary>What the rules saw and did, read back once the repository's transaction is over.</summary>
        private sealed class DayChange
        {
            public PlayerProfile? Profile { get; set; }
            public bool Done { get; set; }
            public HabitCompletion? Completion { get; set; }
        }

        private static HabitProgressResultDto ToResultDto(Habit habit, DateOnly date, HabitProgressSaved saved, DayChange change)
        {
            var checkIn = saved.Effects.CheckIn;

            return new HabitProgressResultDto(
                habit.Id!.Value,
                date,
                saved.DayEntries.Sum(entry => entry.Value),
                change.Done,
                [.. saved.DayEntries.Select(HabitProgressEntryDto.From)],
                habit.CurrentStreak,
                habit.LongestStreak,
                checkIn is null ? null : WalletChangeDto.From(change.Profile!, checkIn.Outcome),
                change.Completion?.MilestoneDays,
                change.Completion?.MilestoneCoins ?? 0,
                change.Completion?.FreezesEarned ?? 0);
        }
    }
}
