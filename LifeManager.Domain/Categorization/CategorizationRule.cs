using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Categorization.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Categorization
{
    /// <summary>
    /// What the user decided for a merchant in their confirmed imports, counted: "lines of <c>ifood restaurante</c> went
    /// to Restaurants 12 times". One row per merchant key, type, action and category, so a merchant the user split
    /// between categories keeps each choice with its own count. Rows are only ever written by an import's confirmation
    /// (an upsert that adds to <see cref="Hits"/>), never edited by hand.
    /// </summary>
    public class CategorizationRule
    {
        public CategorizationRuleId? Id { get; private set; }
        public UserId UserId { get; }

        /// <summary>See <see cref="Categorization.MerchantKey"/>.</summary>
        public string MerchantKey { get; }

        public MoneyFlowType Type { get; }
        public RuleAction Action { get; }

        /// <summary>Null for <see cref="RuleAction.Ignore"/>.</summary>
        public CategoryId? CategoryId { get; }

        public int Hits { get; private set; }
        public DateOnly LastUsedOn { get; private set; }

        private CategorizationRule(UserId userId, string merchantKey, MoneyFlowType type, RuleAction action, CategoryId? categoryId, int hits, DateOnly lastUsedOn)
        {
            UserId = userId;
            MerchantKey = merchantKey;
            Type = type;
            Action = action;
            CategoryId = categoryId;
            Hits = hits;
            LastUsedOn = lastUsedOn;
        }

        /// <summary>Rehydrates a stored rule, so tests and the repository mock can seed and copy any state.</summary>
        internal static CategorizationRule FromPersistence(int id, int idUser, string merchantKey, MoneyFlowType type, RuleAction action, int? idCategory, int hits, DateOnly lastUsedOn)
        {
            var rule = new CategorizationRule(
                new UserId(idUser), merchantKey, type, action, idCategory is null ? null : new CategoryId(idCategory.Value), hits, lastUsedOn);
            rule.Id = new CategorizationRuleId(id);

            return rule;
        }

        /// <summary>Adds what one confirmation taught (the upsert's "Hits + n", for the repository mock).</summary>
        internal void Reinforce(int hits, DateOnly usedOn)
        {
            Hits += hits;
            if (usedOn > LastUsedOn)
                LastUsedOn = usedOn;
        }

        public CategoryEvidence ToEvidence()
            => new(MerchantKey, Type, Action, CategoryId, Hits, LastUsedOn, IsRule: true);
    }
}
