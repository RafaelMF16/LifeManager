using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Transactions;

namespace LifeManager.Domain.Categorization
{
    /// <summary>
    /// One thing the user did that says how a merchant's lines should be filed: a learned rule, or a group of their
    /// transactions with the same merchant key, type and category.
    /// </summary>
    /// <param name="Count">How many times (rule hits, or transactions).</param>
    /// <param name="IsRule">A rule from an import is worth more than a matching transaction, whose description the user may have rewritten.</param>
    public record CategoryEvidence(
        string MerchantKey,
        MoneyFlowType Type,
        RuleAction Action,
        CategoryId? CategoryId,
        int Count,
        DateOnly LastUsedOn,
        bool IsRule)
    {
        /// <summary>
        /// The user's transactions, whether typed by hand or imported: their descriptions are read as merchants, so
        /// "Mercado" typed every month already suggests a category for a line "COMPRA CARTAO MERCADO 12/03".
        /// </summary>
        public static IEnumerable<CategoryEvidence> FromHistory(IEnumerable<CategoryHistoryEntry> history)
            => history
                .Select(entry => (Key: Categorization.MerchantKey.From(entry.NormalizedDescription), Entry: entry))
                .Where(item => item.Key.Length > 0)
                .GroupBy(item => (item.Key, item.Entry.Type, CategoryId: item.Entry.CategoryId.Value))
                .Select(group => new CategoryEvidence(
                    group.Key.Key,
                    group.Key.Type,
                    RuleAction.Categorize,
                    new CategoryId(group.Key.CategoryId),
                    group.Sum(item => item.Entry.Count),
                    group.Max(item => item.Entry.LastDate),
                    IsRule: false));
    }
}
