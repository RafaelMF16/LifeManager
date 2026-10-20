namespace LifeManager.Domain.StatementImports.Enums
{
    public enum StatementImportRowFilter
    {
        All = 1,

        /// <summary>Included but still without a category: what blocks the confirmation.</summary>
        Uncategorized = 2,

        /// <summary>Filed by a suggestion the user hasn't touched yet.</summary>
        Suggested = 3,

        /// <summary>Already among the user's transactions (or likely so).</summary>
        Duplicates = 4,

        /// <summary>Left out of the import.</summary>
        Excluded = 5
    }
}
