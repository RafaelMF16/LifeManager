using LifeManager.Domain.SavingsGoals;
using LifeManager.Domain.SavingsGoals.Interfaces;
using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.SavingsGoals
{
    public class SavingsGoalRepository(LifeManagerDbContext dbContext) : ISavingsGoalRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<SavingsGoal> AddAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken)
        {
            _dbContext.Add(savingsGoal);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return savingsGoal;
        }

        public async Task<SavingsGoal?> GetByIdAsync(SavingsGoalId savingsGoalId, UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.SavingsGoals
                .AsNoTracking()
                .SingleOrDefaultAsync(goal => goal.Id == savingsGoalId && goal.UserId == userId, cancellationToken);
        }

        public async Task<bool> ExistsActiveByNameAsync(UserId userId, SavingsGoalName name, SavingsGoalId? ignoredSavingsGoalId, CancellationToken cancellationToken)
        {
            var normalizedName = name.NormalizedValue;
            var query = _dbContext.SavingsGoals
                .Where(goal => goal.UserId == userId && goal.NormalizedName == normalizedName && goal.ArchivedAt == null);

            if (ignoredSavingsGoalId is not null)
                query = query.Where(goal => goal.Id != ignoredSavingsGoalId);

            return await query.AnyAsync(cancellationToken);
        }
    }
}
