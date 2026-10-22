using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Categorization
{
    public class CategorizationRuleRepository(LifeManagerDbContext dbContext) : ICategorizationRuleRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<IReadOnlyList<CategorizationRule>> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.CategorizationRules
                .AsNoTracking()
                .Where(rule => rule.UserId == userId)
                .ToListAsync(cancellationToken);
        }
    }
}
