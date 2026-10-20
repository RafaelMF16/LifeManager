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
        /// duplicate, which says nothing about the merchant) as one "ignore". An included line at the end of its month
        /// (<see cref="StatementImportRow.IsInShiftWindow"/>) also counts as one "next month" or "same month".
        /// </summary>
        public static IReadOnlyList<RuleLearning> From(IEnumerable<StatementImportRow> rows)
        {
            var learnable = rows.Where(row => row.MerchantKey.Length > 0).ToList();

            var categories = learnable
                .Where(row => row.Included ? row.CategoryId is not null : !row.IsDuplicate)
                .GroupBy(row => (
                    row.MerchantKey,
                    row.Type,
                    Action: row.Included ? RuleAction.Categorize : RuleAction.Ignore,
                    CategoryId: row.Included ? row.CategoryId!.Value : (int?)null));

            var shifts = learnable
                .Where(row => row.Included && StatementImportRow.IsInShiftWindow(row.Date))
                .GroupBy(row => (
                    row.MerchantKey,
                    row.Type,
                    Action: row.ShiftToNextMonth ? RuleAction.NextMonth : RuleAction.SameMonth,
                    CategoryId: (int?)null));

            return [.. categories.Concat(shifts)
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
}
