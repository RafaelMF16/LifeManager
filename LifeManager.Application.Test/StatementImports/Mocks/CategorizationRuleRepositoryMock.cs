using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.StatementImports.Mocks
{
    public class CategorizationRuleRepositoryMock : ICategorizationRuleRepository
    {
        public Task<IReadOnlyList<CategorizationRule>> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken)
        {
            IReadOnlyList<CategorizationRule> rules = [.. CategorizationRuleSingleton.Instance
                .Where(rule => rule.UserId == userId)
                .Select(ToDetachedCopy)];

            return Task.FromResult(rules);
        }

        internal static CategorizationRule ToDetachedCopy(CategorizationRule rule)
            => CategorizationRule.FromPersistence(
                rule.Id!.Value,
                rule.UserId.Value,
                rule.MerchantKey,
                rule.Type,
                rule.Action,
                rule.CategoryId?.Value,
                rule.Hits,
                rule.LastUsedOn);
    }
}
