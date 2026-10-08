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
    /// Relapses of habits to avoid: logging one costs HP on the spot (within a weekly limit, only past it) and breaks the
    /// streak; undoing it gives the HP back. Only today and yesterday can change, like check-ins.
    /// </summary>
    public class HabitRelapseService(
        IHabitRepository habitRepository,
        IHabitCheckInRepository habitCheckInRepository,
        AppClock appClock,
        TimeProvider timeProvider)
    {
        private readonly IHabitRepository _habitRepository = habitRepository;
        private readonly IHabitCheckInRepository _habitCheckInRepository = habitCheckInRepository;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<Result<HabitRelapseResultDto>> RelapseAsync(int id, HabitRelapseDto relapseDto, UserId userId, CancellationToken cancellationToken)
        {
            var habit = await _habitRepository.GetByIdAsync(new HabitId(id), userId, cancellationToken);
            if (habit is null)
                return HabitErrors.NotFound;

            var date = relapseDto.Date;
            var today = _appClock.Today();

            var canRelapse = habit.EnsureCanRelapse(date, today);
            if (!canRelapse.IsSuccess)
                return canRelapse.Error;

            var now = _timeProvider.GetUtcNow();
            PlayerProfile? profile = null;
            int? weekRelapseCount = null;

            var effects = await _habitCheckInRepository.RecordAsync(habit, date, HabitCheckInStatus.Relapse, now, context =>
            {
                profile = context.Profile;
                weekRelapseCount = WeekRelapseCount(habit, context.FailedDates, date);

                var damage = HabitRewards.RelapseDamage(habit, StreakCalculator.CountInWeek(context.FailedDates, date));
                var outcome = context.Profile.Apply(new GameDelta(0, 0, -damage));
                var entries = GameLedgerEntry.FromOutcome(habit.UserId, GameLedgerEntryKind.Relapse, date, now, habit.Name.Value, outcome, habit.Id);
                var streak = StreakCalculator.Current(habit, context.SuccessDates, context.FailedDates, today);

                return new HabitCheckInEffects(streak, outcome.Applied, outcome, entries);
            }, cancellationToken);

            if (effects is null)
                return HabitErrors.AlreadyRelapsed;

            return new HabitRelapseResultDto(
                habit.Id!.Value, date, true, habit.CurrentStreak, habit.LongestStreak, WalletChangeDto.From(profile!, effects.Outcome), weekRelapseCount);
        }

        /// <summary>
        /// Gives back the HP the relapse cost (the coins a knockout took are gone) and recalculates the streak.
        /// </summary>
        public async Task<Result<HabitRelapseResultDto>> UndoRelapseAsync(int id, DateOnly date, UserId userId, CancellationToken cancellationToken)
        {
            var habit = await _habitRepository.GetByIdAsync(new HabitId(id), userId, cancellationToken);
            if (habit is null)
                return HabitErrors.NotFound;

            var today = _appClock.Today();

            var canUndo = habit.EnsureCanRelapse(date, today);
            if (!canUndo.IsSuccess)
                return canUndo.Error;

            var now = _timeProvider.GetUtcNow();
            PlayerProfile? profile = null;
            int? weekRelapseCount = null;

            var effects = await _habitCheckInRepository.RemoveAsync(habit, date, HabitCheckInStatus.Relapse, context =>
            {
                profile = context.Profile;
                weekRelapseCount = WeekRelapseCount(habit, context.FailedDates, date);

                var outcome = context.Profile.Apply(context.Removed!.Awarded.Inverse());
                var entries = GameLedgerEntry.FromOutcome(habit.UserId, GameLedgerEntryKind.Undo, date, now, habit.Name.Value, outcome, habit.Id);
                var streak = StreakCalculator.Current(habit, context.SuccessDates, context.FailedDates, today);

                return new HabitCheckInEffects(streak, outcome.Applied, outcome, entries);
            }, cancellationToken);

            if (effects is null)
                return HabitErrors.RelapseNotFound;

            return new HabitRelapseResultDto(
                habit.Id!.Value, date, false, habit.CurrentStreak, habit.LongestStreak, WalletChangeDto.From(profile!, effects.Outcome), weekRelapseCount);
        }

        private static int? WeekRelapseCount(Habit habit, IReadOnlySet<DateOnly> failedDates, DateOnly date)
            => habit.FrequencyType == HabitFrequencyType.TimesPerWeek ? StreakCalculator.CountInWeek(failedDates, date) : null;
    }
}
