namespace LifeManager.Domain.Categorization.Enums
{
    /// <summary>Where an imported line's category came from.</summary>
    public enum SuggestionSource
    {
        /// <summary>Nothing to go by: the user picks the category.</summary>
        None = 1,

        /// <summary>A rule learned from earlier imports of the same merchant.</summary>
        Rule = 2,

        /// <summary>The user's transactions with the same merchant.</summary>
        History = 3,

        /// <summary>A merchant with a similar name.</summary>
        Similar = 4
    }
}
