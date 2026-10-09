using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Shared.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.Text;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Habits.Services
{
    /// <remarks>
    /// Deleting a habit archives it: its history stays, its name is free for a new habit, and it can be restored as long
    /// as no active habit took that name meanwhile.
    /// </remarks>
    public class HabitService(IHabitRepository habitRepository, AppClock appClock, TimeProvider timeProvider)
    {
        private readonly IHabitRepository _habitRepository = habitRepository;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<Result<HabitResponseDto>> CreateAsync(HabitDto habitDto, UserId userId, CancellationToken cancellationToken)
        {
            var habitResult = Habit.Create(
                userId.Value,
                habitDto.Name,
                habitDto.Description,
                habitDto.Trigger,
                habitDto.Kind,
                habitDto.Difficulty,
                habitDto.FrequencyType,
                HabitWeekDaysMapper.ToMask(habitDto.WeekDays),
                habitDto.TimesPerWeek,
                _appClock.Today(),
                _timeProvider.GetUtcNow());
            if (!habitResult.IsSuccess)
                return habitResult.Error;

            var habit = habitResult.Value;

            if (await _habitRepository.ExistsActiveByNameAsync(userId, habit.Name, null, cancellationToken))
                return HabitErrors.NameAlreadyExists;

            await _habitRepository.AddAsync(habit, cancellationToken);

            return ToResponseDto(habit);
        }

        public async Task<Result<HabitResponseDto>> GetByIdAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var habit = await _habitRepository.GetByIdAsync(new HabitId(id), userId, cancellationToken);
            if (habit is null)
                return HabitErrors.NotFound;

            return ToResponseDto(habit);
        }

        public async Task<Result<PagedResponseDto<HabitResponseDto>>> GetPagedAsync(HabitListQueryDto query, UserId userId, CancellationToken cancellationToken)
        {
            var pageRequestResult = PageRequest.Create(query.Page, query.PageSize);
            if (!pageRequestResult.IsSuccess)
                return pageRequestResult.Error;

            var pageRequest = pageRequestResult.Value;
            var normalizedSearch = SearchText.Normalize(query.Search);

            if (normalizedSearch.Length > HabitName.MaxLength)
                return new PagedResponseDto<HabitResponseDto>([], 0, pageRequest.Page, pageRequest.PageSize, 0);

            var habits = await _habitRepository.GetPagedByUserIdAsync(
                userId, pageRequest, query.Status, normalizedSearch, query.SortBy, query.SortDirection, cancellationToken);

            return PagedResponseDto<HabitResponseDto>.From(habits, ToResponseDto);
        }

        public async Task<Result<HabitResponseDto>> UpdateAsync(int id, HabitUpdateDto habitDto, UserId userId, CancellationToken cancellationToken)
        {
            var habit = await _habitRepository.GetByIdAsync(new HabitId(id), userId, cancellationToken);
            if (habit is null)
                return HabitErrors.NotFound;

            var currentNormalizedName = habit.NormalizedName;

            var updateResult = habit.Update(
                habitDto.Name,
                habitDto.Description,
                habitDto.Trigger,
                habitDto.Difficulty,
                habitDto.FrequencyType,
                HabitWeekDaysMapper.ToMask(habitDto.WeekDays),
                habitDto.TimesPerWeek);
            if (!updateResult.IsSuccess)
                return updateResult.Error;

            if (habit.NormalizedName != currentNormalizedName
                && await _habitRepository.ExistsActiveByNameAsync(userId, habit.Name, habit.Id, cancellationToken))
                return HabitErrors.NameAlreadyExists;

            await _habitRepository.UpdateAsync(habit, cancellationToken);

            return ToResponseDto(habit);
        }

        public async Task<Result> ArchiveAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var habit = await _habitRepository.GetByIdAsync(new HabitId(id), userId, cancellationToken);
            if (habit is null)
                return HabitErrors.NotFound;

            var archiveResult = habit.Archive(_timeProvider.GetUtcNow());
            if (!archiveResult.IsSuccess)
                return archiveResult.Error;

            await _habitRepository.UpdateAsync(habit, cancellationToken);

            return Result.Success();
        }

        public async Task<Result<HabitResponseDto>> RestoreAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var habit = await _habitRepository.GetByIdAsync(new HabitId(id), userId, cancellationToken);
            if (habit is null)
                return HabitErrors.NotFound;

            var restoreResult = habit.Restore(_appClock.Today());
            if (!restoreResult.IsSuccess)
                return restoreResult.Error;

            if (await _habitRepository.ExistsActiveByNameAsync(userId, habit.Name, habit.Id, cancellationToken))
                return HabitErrors.NameAlreadyExists;

            await _habitRepository.UpdateAsync(habit, cancellationToken);

            return ToResponseDto(habit);
        }

        private static HabitResponseDto ToResponseDto(Habit habit)
            => HabitResponseDto.From(habit);
    }
}
