using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.StatementImports;

namespace LifeManager.Domain.Categorization
{
    /// <summary>What one confirmed import teaches: hits to add to a <see cref="CategorizationRule"/> (created if missing).</summary>
    public record RuleLearning(string MerchantKey, MoneyFlowType Type, RuleAction Action, CategoryId? CategoryId, int Hits)
    {
        /// <summary>
        /// Each included line counts as one use of its category for its merchant, and each line left out (other than a
        /// duplicate, which says nothing about the merchant) as one "ignore".
        /// </summary>
        public static IReadOnlyList<RuleLearning> From(IEnumerable<StatementImportRow> rows)
            => [.. rows
                .Where(row => row.MerchantKey.Length > 0 && (row.Included ? row.CategoryId is not null : !row.IsDuplicate))
                .GroupBy(row => (
                    row.MerchantKey,
                    row.Type,
                    Action: row.Included ? RuleAction.Categorize : RuleAction.Ignore,
                    CategoryId: row.Included ? row.CategoryId!.Value : (int?)null))
                .Select(group => new RuleLearning(
                    group.Key.MerchantKey,
                    group.Key.Type,
                    group.Key.Action,
                    group.Key.CategoryId is null ? null : new CategoryId(group.Key.CategoryId.Value),
                    group.Count()))
                .OrderBy(learning => learning.MerchantKey, StringComparer.Ordinal)
                .ThenBy(learning => learning.Type)
                .ThenBy(learning => learning.Action)
                .ThenBy(learning => learning.CategoryId?.Value)];
    }
}
