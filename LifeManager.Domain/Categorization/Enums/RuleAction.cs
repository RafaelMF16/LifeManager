namespace LifeManager.Domain.Categorization.Enums
{
    /// <summary>
    /// What the user did with a merchant's lines: gave them a category, or left them out of the import. Separately, for
    /// lines at the end of their month, whether they were counted in the next month.
    /// </summary>
    public enum RuleAction
    {
        Categorize = 1,

        /// <summary>Not imported (e.g. a transfer between the user's own accounts).</summary>
        Ignore = 2,

        /// <summary>Counted in the month after its date (a salary paid on the last business day). Not a category decision.</summary>
        NextMonth = 3,

        /// <summary>Kept in the month of its date although it was at the end of the month. Not a category decision.</summary>
        SameMonth = 4
    }
}
