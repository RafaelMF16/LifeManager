using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Domain.Categorization
{
    /// <summary>
    /// Suggests how to file an imported line from what the user did before (<see cref="CategoryEvidence"/>). Built once
    /// per import over all the user's evidence, then asked once per merchant key.
    /// <list type="number">
    /// <item>Same merchant key: the decision with the highest score wins. A score is the number of uses, doubled for
    /// learned rules and fading with age (half weight after a year). <see cref="SuggestionConfidence.High"/> when it
    /// was used at least twice and holds at least 80% of the score.</item>
    /// <item>Otherwise, merchants with a similar key (<see cref="TrigramSimilarity"/> ≥ <see cref="MinSimilarity"/>),
    /// each weighted by how similar it is. At most <see cref="SuggestionConfidence.Medium"/>, and never "ignore":
    /// leaving out a line because another merchant was left out is too risky.</item>
    /// </list>
    /// Only decisions in the line's direction count: money going out may be an expense or an investment, money coming
    /// in is income.
    /// </summary>
    public sealed class CategorySuggester
    {
        public const double MinSimilarity = 0.5;
        public const double StrongSimilarity = 0.7;
        public const double DominantShare = 0.8;
        public const double RuleWeight = 2;

        private readonly DateOnly _today;
        private readonly Dictionary<string, List<CategoryEvidence>> _byKey;
        private readonly List<(string Key, HashSet<string> Trigrams)> _keys;
        private readonly Dictionary<(string Key, bool IsIncome), CategorySuggestion?> _cache = [];

        public CategorySuggester(IEnumerable<CategoryEvidence> evidence, DateOnly today)
        {
            _today = today;

            // What was learned about the month (next month / same month) says nothing about the category.
            _byKey = evidence
                .Where(item => item.MerchantKey.Length > 0 && item.Count > 0 && item.Action is RuleAction.Categorize or RuleAction.Ignore)
                .GroupBy(item => item.MerchantKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
            _keys = [.. _byKey.Keys.Select(key => (key, TrigramSimilarity.Trigrams(key)))];
        }

        /// <param name="isIncome">Whether the line brings money in (a positive amount on the statement).</param>
        public CategorySuggestion? Suggest(string merchantKey, bool isIncome)
        {
            if (_cache.TryGetValue((merchantKey, isIncome), out var cached))
                return cached;

            var suggestion = SuggestFromSameKey(merchantKey, isIncome) ?? SuggestFromSimilarKeys(merchantKey, isIncome);
            _cache[(merchantKey, isIncome)] = suggestion;

            return suggestion;
        }

        private CategorySuggestion? SuggestFromSameKey(string merchantKey, bool isIncome)
        {
            if (!_byKey.TryGetValue(merchantKey, out var evidence))
                return null;

            var candidates = Tally(evidence.Where(item => InDirection(item, isIncome)).Select(item => (item, 1.0)));
            if (candidates.Count == 0)
                return null;

            var (best, share) = Best(candidates);
            var confidence = best.Uses >= 2 && share >= DominantShare ? SuggestionConfidence.High : SuggestionConfidence.Medium;
            var source = best.FromRule ? SuggestionSource.Rule : SuggestionSource.History;

            return new CategorySuggestion(best.Action, best.Type, best.CategoryId, source, confidence, best.Uses);
        }

        private CategorySuggestion? SuggestFromSimilarKeys(string merchantKey, bool isIncome)
        {
            var trigrams = TrigramSimilarity.Trigrams(merchantKey);
            var weighted = new List<(CategoryEvidence Item, double Weight)>();
            var bestSimilarity = 0.0;

            foreach (var (key, keyTrigrams) in _keys)
            {
                var similarity = TrigramSimilarity.Similarity(trigrams, keyTrigrams);
                if (similarity < MinSimilarity)
                    continue;

                var matching = _byKey[key].Where(item => item.Action == RuleAction.Categorize && InDirection(item, isIncome)).ToList();
                if (matching.Count == 0)
                    continue;

                bestSimilarity = Math.Max(bestSimilarity, similarity);
                weighted.AddRange(matching.Select(item => (item, similarity)));
            }

            var candidates = Tally(weighted);
            if (candidates.Count == 0)
                return null;

            var (best, share) = Best(candidates);
            var confidence = share >= DominantShare && bestSimilarity >= StrongSimilarity ? SuggestionConfidence.Medium : SuggestionConfidence.Low;

            return new CategorySuggestion(best.Action, best.Type, best.CategoryId, SuggestionSource.Similar, confidence, best.Uses);
        }

        private List<Candidate> Tally(IEnumerable<(CategoryEvidence Item, double Weight)> evidence)
            => [.. evidence
                .GroupBy(entry => (entry.Item.Action, entry.Item.Type, CategoryId: entry.Item.CategoryId?.Value))
                .Select(group => new Candidate(
                    group.Key.Action,
                    group.Key.Type,
                    group.Key.CategoryId is null ? null : new CategoryId(group.Key.CategoryId.Value),
                    group.Sum(entry => Score(entry.Item) * entry.Weight),
                    group.Sum(entry => entry.Item.Count),
                    group.Any(entry => entry.Item.IsRule)))];

        /// <summary>Highest score; ties go to the most used, then the lowest category id, so the result is stable.</summary>
        private static (Candidate Best, double Share) Best(List<Candidate> candidates)
        {
            var best = candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => candidate.Uses)
                .ThenBy(candidate => candidate.CategoryId?.Value ?? int.MaxValue)
                .First();

            var total = candidates.Sum(candidate => candidate.Score);
            return (best, total == 0 ? 0 : best.Score / total);
        }

        private double Score(CategoryEvidence item)
            => item.Count * (item.IsRule ? RuleWeight : 1) * Recency(item.LastUsedOn, _today);

        /// <summary>How much a decision last used on <paramref name="lastUsedOn"/> still counts: half after a year.</summary>
        internal static double Recency(DateOnly lastUsedOn, DateOnly today)
        {
            var ageInMonths = Math.Max(0, today.DayNumber - lastUsedOn.DayNumber) / 30.0;

            return 1 / (1 + ageInMonths / 12);
        }

        private static bool InDirection(CategoryEvidence item, bool isIncome)
            => isIncome ? item.Type == MoneyFlowType.Income : item.Type != MoneyFlowType.Income;

        private sealed record Candidate(RuleAction Action, MoneyFlowType Type, CategoryId? CategoryId, double Score, int Uses, bool FromRule);
    }
}
