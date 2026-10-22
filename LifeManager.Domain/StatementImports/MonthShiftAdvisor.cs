using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Domain.StatementImports
{
    /// <summary>
    /// Whether a new import row should count in the next month, from what the user decided for the same merchant and
    /// type at the end of earlier months (<see cref="RuleAction.NextMonth"/> / <see cref="RuleAction.SameMonth"/>). Only an
    /// included row in the last days of its month (<see cref="StatementImportRow.IsInShiftWindow"/>) is shifted, so a
    /// salary that arrives late, or another payment of the same merchant mid-month, stays in its own month. The heavier
    /// decision wins, weighted by recency like category suggestions; a tie keeps the row in its month.
    /// </summary>
    public sealed class MonthShiftAdvisor
    {
        private readonly Dictionary<(string MerchantKey, MoneyFlowType Type), double> _scores;

        public MonthShiftAdvisor(IEnumerable<CategorizationRule> rules, DateOnly today)
        {
            _scores = rules
                .Where(rule => rule.Action is RuleAction.NextMonth or RuleAction.SameMonth)
                .GroupBy(rule => (rule.MerchantKey, rule.Type))
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(rule => (rule.Action == RuleAction.NextMonth ? 1 : -1) * rule.Hits * CategorySuggester.Recency(rule.LastUsedOn, today)));
        }

        public bool ShouldShift(StatementImportRow row)
            => row.Included
                && StatementImportRow.IsInShiftWindow(row.Date)
                && _scores.TryGetValue((row.MerchantKey, row.Type), out var score)
                && score > 0;
    }
}
