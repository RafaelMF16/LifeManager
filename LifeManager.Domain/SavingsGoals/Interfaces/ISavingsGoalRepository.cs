using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.SavingsGoals.Interfaces
{
    public interface ISavingsGoalRepository
    {
        Task<SavingsGoal> AddAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken);

        /// <summary>Archived goals included.</summary>
        Task<SavingsGoal?> GetByIdAsync(SavingsGoalId savingsGoalId, UserId userId, CancellationToken cancellationToken);

        /// <summary>
        /// Whether another active (not archived) goal of the user has this name; case- and accent-insensitive, comparing
        /// <see cref="SavingsGoalName.NormalizedValue"/>.
        /// </summary>
        Task<bool> ExistsActiveByNameAsync(UserId userId, SavingsGoalName name, SavingsGoalId? ignoredSavingsGoalId, CancellationToken cancellationToken);
    }
}
