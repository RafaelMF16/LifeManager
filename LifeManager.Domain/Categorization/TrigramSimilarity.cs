namespace LifeManager.Domain.Categorization
{
    /// <summary>
    /// The same measure as PostgreSQL's pg_trgm <c>similarity()</c>: each word padded with two spaces before and one after
    /// is cut into three-letter pieces, and the result is shared pieces / all distinct pieces (0 = nothing in common,
    /// 1 = same words). Used on merchant keys, which are already normalized (lowercase, no accents).
    /// </summary>
    public static class TrigramSimilarity
    {
        public static HashSet<string> Trigrams(string value)
        {
            var trigrams = new HashSet<string>(StringComparer.Ordinal);

            foreach (var word in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var padded = $"  {word} ";
                for (var index = 0; index + 3 <= padded.Length; index++)
                    trigrams.Add(padded.Substring(index, 3));
            }

            return trigrams;
        }

        public static double Similarity(string first, string second)
            => Similarity(Trigrams(first), Trigrams(second));

        public static double Similarity(IReadOnlySet<string> first, IReadOnlySet<string> second)
        {
            if (first.Count == 0 || second.Count == 0)
                return 0;

            var shared = first.Count(second.Contains);
            return (double)shared / (first.Count + second.Count - shared);
        }
    }
}
