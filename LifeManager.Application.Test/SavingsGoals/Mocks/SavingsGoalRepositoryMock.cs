using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.SavingsGoals;
using LifeManager.Domain.SavingsGoals.Interfaces;
using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.SavingsGoals.Mocks
{
    public class SavingsGoalRepositoryMock : ISavingsGoalRepository
    {
        private readonly SavingsGoalSingleton _instance = SavingsGoalSingleton.Instance;

        public Task<SavingsGoal> AddAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken)
        {
            var newId = _instance.Count == 0 ? 1 : _instance.Max(storedGoal => storedGoal.Id!.Value) + 1;
            savingsGoal.AssignId(newId);

            _instance.Add(ToDetachedCopy(savingsGoal));

            return Task.FromResult(savingsGoal);
        }

        public Task<SavingsGoal?> GetByIdAsync(SavingsGoalId savingsGoalId, UserId userId, CancellationToken cancellationToken)
        {
            var storedGoal = _instance.SingleOrDefault(goal => goal.Id == savingsGoalId && goal.UserId == userId);

            return Task.FromResult(storedGoal is null ? null : ToDetachedCopy(storedGoal));
        }

        public Task<bool> ExistsActiveByNameAsync(UserId userId, SavingsGoalName name, SavingsGoalId? ignoredSavingsGoalId, CancellationToken cancellationToken)
        {
            var exists = _instance.Any(goal =>
                goal.UserId == userId
                && !goal.IsArchived
                && goal.NormalizedName == name.NormalizedValue
                && (ignoredSavingsGoalId is null || goal.Id != ignoredSavingsGoalId));

            return Task.FromResult(exists);
        }

        internal static SavingsGoal ToDetachedCopy(SavingsGoal goal)
            => SavingsGoal.FromPersistence(
                goal.Id!.Value, goal.UserId.Value, goal.Name.Value, goal.TargetAmount, goal.TargetMonthStart, goal.InitialAmount, goal.Icon,
                goal.CreatedAt, goal.ArchivedAt, goal.CompletedAt);
    }
}
