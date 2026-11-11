using LifeManager.Application.Shared.Time;
using LifeManager.Domain.SavingsGoals;
using LifeManager.Domain.SavingsGoals.Interfaces;
using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.SavingsGoals.Services
{
    /// <summary>
    /// Records <see cref="SavingsGoal.CompletedAt"/> the first time a write takes the goal to its target: a linked
    /// transaction created or edited, or the goal itself created or edited. Done on writes, not reads, so the
    /// "completed" listing filter (a column) is never behind. Never cleared.
    /// </summary>
    public class SavingsGoalCompletion(ISavingsGoalRepository savingsGoalRepository, AppClock appClock)
    {
        private readonly ISavingsGoalRepository _savingsGoalRepository = savingsGoalRepository;
        private readonly AppClock _appClock = appClock;

        /// <summary>For a goal already in hand with its sums; sets <see cref="SavingsGoal.CompletedAt"/> on it too.</summary>
        public async Task RecordIfReachedAsync(SavingsGoal goal, SavingsGoalSums sums, CancellationToken cancellationToken)
        {
            if (goal.CompletedAt is not null || !SavingsGoalProgress.Reaches(goal.TargetAmount, goal.InitialAmount, sums.Total))
                return;

            var today = _appClock.Today();
            await _savingsGoalRepository.MarkCompletedAsync(goal.Id!, today, cancellationToken);
            goal.MarkCompleted(today);
        }

        /// <summary>After a transaction linked to the goal was written.</summary>
        public async Task RecordIfReachedAsync(SavingsGoalId savingsGoalId, UserId userId, CancellationToken cancellationToken)
        {
            var goal = await _savingsGoalRepository.GetByIdAsync(savingsGoalId, userId, cancellationToken);
            if (goal is null || goal.CompletedAt is not null)
                return;

            // The recent part isn't needed here; any day works.
            var sums = await _savingsGoalRepository.GetLinkedSumsAsync([savingsGoalId], DateOnly.MaxValue, cancellationToken);

            await RecordIfReachedAsync(goal, sums.GetValueOrDefault(savingsGoalId, SavingsGoalSums.Zero), cancellationToken);
        }
    }
}
