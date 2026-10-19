using LifeManager.Domain.Categorization;
using LifeManager.Domain.Transactions;

namespace LifeManager.Domain.StatementImports
{
    /// <summary>
    /// Leaves out the statement lines the user already has, each existing transaction matching at most one line:
    /// <list type="number">
    /// <item>the same bank line id (<see cref="ImportExternalId"/>): it came from an earlier import;</item>
    /// <item>the same date and signed amount (a transaction typed by hand), preferring the closest description;</item>
    /// <item>the same signed amount up to <see cref="NearbyDays"/> days apart, with a similar merchant
    /// (<see cref="TrigramSimilarity"/> ≥ <see cref="MinSimilarity"/>): banks often post a card purchase a day or two later.</item>
    /// </list>
    /// Two equal lines in the statement (two coffees on the same day) stay apart: only one of them can match.
    /// </summary>
    public static class DuplicateDetector
    {
        public const int NearbyDays = 3;
        public const double MinSimilarity = 0.5;

        public static void MarkDuplicates(IReadOnlyList<StatementImportRow> rows, IReadOnlyList<ExistingTransaction> existing)
        {
            var available = existing.ToList();

            var byExternalId = available
                .Where(transaction => transaction.ExternalId is not null)
                .GroupBy(transaction => transaction.ExternalId!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            foreach (var row in rows.Where(row => row.ExternalId is not null))
            {
                if (!byExternalId.Remove(row.ExternalId!, out var match))
                    continue;

                row.MarkDuplicateOf(match.Id, match.MonthlySummaryId);
                available.Remove(match);
            }

            var pending = rows.Where(row => !row.IsDuplicate).OrderBy(row => row.Date).ToList();
            var keys = available.ToDictionary(transaction => transaction, transaction => MerchantKey.From(transaction.NormalizedDescription));
            var byAmount = available
                .GroupBy(transaction => transaction.SignedAmount)
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (var row in pending)
            {
                if (!byAmount.TryGetValue(row.SignedAmount, out var sameAmount) || sameAmount.Count == 0)
                    continue;

                var match = sameAmount
                    .Where(transaction => transaction.Date == row.Date)
                    .OrderByDescending(transaction => TrigramSimilarity.Similarity(keys[transaction], row.MerchantKey))
                    .FirstOrDefault()
                    ?? sameAmount
                        .Select(transaction => (Transaction: transaction, Similarity: TrigramSimilarity.Similarity(keys[transaction], row.MerchantKey)))
                        .Where(candidate => Math.Abs(candidate.Transaction.Date.DayNumber - row.Date.DayNumber) <= NearbyDays && candidate.Similarity >= MinSimilarity)
                        .OrderByDescending(candidate => candidate.Similarity)
                        .ThenBy(candidate => Math.Abs(candidate.Transaction.Date.DayNumber - row.Date.DayNumber))
                        .Select(candidate => candidate.Transaction)
                        .FirstOrDefault();

                if (match is null)
                    continue;

                row.MarkDuplicateOf(match.Id, match.MonthlySummaryId);
                sameAmount.Remove(match);
            }
        }
    }
}
