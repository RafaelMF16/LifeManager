namespace LifeManager.Domain.Categorization.Enums
{
    /// <summary>What the user did with a merchant's lines: gave them a category, or left them out of the import.</summary>
    public enum RuleAction
    {
        Categorize = 1,

        /// <summary>Not imported (e.g. a transfer between the user's own accounts).</summary>
        Ignore = 2
    }
}
