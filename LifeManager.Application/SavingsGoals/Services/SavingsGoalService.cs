using LifeManager.Application.SavingsGoals.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.SavingsGoals;
using LifeManager.Domain.SavingsGoals.Errors;
using LifeManager.Domain.SavingsGoals.Interfaces;
using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.SavingsGoals.Services
{
    /// <remarks>
    /// Deleting a goal archives it: its investments keep pointing at it, its name is free for a new goal, and it can be
    /// restored as long as no active goal took that name meanwhile.
    /// </remarks>
    public class SavingsGoalService(ISavingsGoalRepository savingsGoalRepository, AppClock appClock, TimeProvider timeProvider)
    {
        private readonly ISavingsGoalRepository _savingsGoalRepository = savingsGoalRepository;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<Result<SavingsGoalResponseDto>> CreateAsync(SavingsGoalDto goalDto, UserId userId, CancellationToken cancellationToken)
        {
            if (!TryParseTargetMonth(goalDto.TargetMonth, out var targetMonth))
                return SavingsGoalErrors.InvalidTargetMonth;

            var goalResult = SavingsGoal.Create(
                userId.Value,
                goalDto.Name,
                goalDto.TargetAmount,
                targetMonth,
                goalDto.InitialAmount,
                goalDto.Icon,
                CurrentMonth(),
                _timeProvider.GetUtcNow());
            if (!goalResult.IsSuccess)
                return goalResult.Error;

            var goal = goalResult.Value!;

            if (await _savingsGoalRepository.ExistsActiveByNameAsync(userId, goal.Name, null, cancellationToken))
                return SavingsGoalErrors.NameAlreadyExists;

            await _savingsGoalRepository.AddAsync(goal, cancellationToken);

            return ToResponseDto(goal);
        }

        public async Task<Result<SavingsGoalResponseDto>> GetByIdAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var goal = await _savingsGoalRepository.GetByIdAsync(new SavingsGoalId(id), userId, cancellationToken);
            if (goal is null)
                return SavingsGoalErrors.NotFound;

            return ToResponseDto(goal);
        }

        private YearMonth CurrentMonth() => YearMonth.From(_appClock.Today());

        /// <summary>Null or blank means no deadline; anything else must be "yyyy-MM".</summary>
        private static bool TryParseTargetMonth(string? value, out YearMonth? targetMonth)
        {
            targetMonth = null;
            if (string.IsNullOrWhiteSpace(value))
                return true;

            if (!YearMonth.TryParse(value.Trim(), out var month))
                return false;

            targetMonth = month;
            return true;
        }

        private static SavingsGoalResponseDto ToResponseDto(SavingsGoal goal)
            => new(
                goal.Id!.Value,
                goal.Name.Value,
                goal.TargetAmount,
                goal.TargetMonth?.ToString(),
                goal.InitialAmount,
                goal.Icon,
                goal.CreatedAt,
                goal.ArchivedAt,
                goal.CompletedAt);
    }
}
