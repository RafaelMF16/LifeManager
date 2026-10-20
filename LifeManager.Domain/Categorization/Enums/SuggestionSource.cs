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
        Similar = 4,

        /// <summary>The bank's line for money put into an investment (<see cref="StatementImports.InvestmentDetector"/>).</summary>
        InvestmentApplication = 5,

        /// <summary>The bank's line for money taken out of an investment, left out (<see cref="StatementImports.InvestmentDetector"/>).</summary>
        InvestmentRedemption = 6,

        /// <summary>A transfer between the user's own accounts, left out.</summary>
        OwnTransfer = 7
    }
}
