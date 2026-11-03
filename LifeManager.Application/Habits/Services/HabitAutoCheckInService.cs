using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LifeManager.Application.Habits.Services
{
    /// <summary>
    /// Checks in, today, the habits a finance action triggers (<see cref="Habit.AutoTrigger"/>), with the same reward,
    /// milestone and freeze as a check-in by hand. Called after the finance write succeeded: it never fails it. A refusal
    /// is skipped, and an exception is logged and ends the run, keeping what was already checked in.
    /// </summary>
    public class HabitAutoCheckInService(
        IHabitRepository habitRepository,
        IHabitCheckInRepository habitCheckInRepository,
        HabitCheckInService habitCheckInService,
        AppClock appClock,
        ILogger<HabitAutoCheckInService> logger)
    {
        private readonly IHabitRepository _habitRepository = habitRepository;
        private readonly IHabitCheckInRepository _habitCheckInRepository = habitCheckInRepository;
        private readonly HabitCheckInService _habitCheckInService = habitCheckInService;
        private readonly AppClock _appClock = appClock;
        private readonly ILogger<HabitAutoCheckInService> _logger = logger;

        /// <returns>The habits checked in now; empty when none was due or all were already checked in today.</returns>
        public async Task<IReadOnlyList<AutoCheckInDto>> OnEventAsync(UserId userId, HabitAutoTrigger trigger, CancellationToken cancellationToken)
        {
            List<AutoCheckInDto> checkedIn = [];

            try
            {
                var habits = await _habitRepository.GetAutoTriggeredAsync(userId, trigger, cancellationToken);
                if (habits.Count == 0)
                    return checkedIn;

                var today = _appClock.Today();
                var weekCheckIns = await _habitCheckInRepository.GetByUserIdAsync(userId, StreakCalculator.WeekStart(today), today, cancellationToken);

                foreach (var habit in habits.Where(habit => IsOpenToday(habit, weekCheckIns, today)))
                {
                    var result = await _habitCheckInService.CheckInAsync(habit.Id!.Value, new HabitCheckInDto(today), userId, cancellationToken);
                    if (result.IsSuccess)
                        checkedIn.Add(AutoCheckInDto.From(habit.Name.Value, result.Value));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Automatic check-ins for {Trigger} stopped: the request was aborted", trigger);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Automatic check-ins for {Trigger} failed for user {UserId}", trigger, userId.Value);
            }

            return checkedIn;
        }

        /// <summary>Due today, without a check-in today and, for a times-per-week habit, short of its week's target.</summary>
        private static bool IsOpenToday(Habit habit, IReadOnlyList<HabitCheckIn> weekCheckIns, DateOnly today)
        {
            if (habit.Target is not null || !habit.EnsureCanCheckIn(today, today).IsSuccess)
                return false;

            var habitWeek = weekCheckIns.Where(checkIn => checkIn.HabitId == habit.Id).ToList();
            if (habitWeek.Any(checkIn => checkIn.Date == today))
                return false;

            return habit.FrequencyType != HabitFrequencyType.TimesPerWeek
                || habitWeek.Count(checkIn => checkIn.IsSuccess) < habit.TimesPerWeek;
        }
    }
}
