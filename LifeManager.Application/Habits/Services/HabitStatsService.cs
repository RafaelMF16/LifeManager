using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Habits.Services
{
    /// <summary>A habit's history for its details screen: the heatmap, the recent consistency and the days kept.</summary>
    public class HabitStatsService(IHabitRepository habitRepository, IHabitCheckInRepository habitCheckInRepository, AppClock appClock)
    {
        private readonly IHabitRepository _habitRepository = habitRepository;
        private readonly IHabitCheckInRepository _habitCheckInRepository = habitCheckInRepository;
        private readonly AppClock _appClock = appClock;

        /// <remarks>Archived habits too: their history stays.</remarks>
        public async Task<Result<HabitStatsDto>> GetAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var habit = await _habitRepository.GetByIdAsync(new HabitId(id), userId, cancellationToken);
            if (habit is null)
                return HabitErrors.NotFound;

            var today = _appClock.Today();
            var checkIns = await _habitCheckInRepository.GetByHabitAsync(habit.Id!, HabitStats.HistoryStart(today), today, cancellationToken);
            var totalKept = await _habitCheckInRepository.CountKeptAsync(habit.Id!, cancellationToken);

            return new HabitStatsDto(
                HabitResponseDto.From(habit),
                today,
                [.. HabitStats.Days(habit, checkIns, today).Select(day => new HabitDayDto(day.Date, day.State))],
                HabitConsistencyDto.From(HabitStats.Consistency(habit, checkIns, today)),
                totalKept);
        }
    }
}
