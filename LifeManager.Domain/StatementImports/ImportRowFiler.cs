using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.StatementImports.Enums;

namespace LifeManager.Domain.StatementImports
{
    /// <summary>
    /// Files a new import row before the user reviews it. What the user did with this same merchant comes first
    /// (<see cref="SuggestionSource.Rule"/>, <see cref="SuggestionSource.History"/>); then what the bank's own wording says:
    /// an investment movement (<see cref="InvestmentDetector"/>), then a transfer in the user's own name
    /// (<see cref="OwnTransferDetector"/>); then a merchant with a similar name. Otherwise the row stays as the bank's sign
    /// says. A detected application keeps the category of a similar investment, if there is one.
    /// </summary>
    public static class ImportRowFiler
    {
        /// <param name="suggestion">The <see cref="CategorySuggester"/>'s answer for the row's merchant key and direction.</param>
        public static void File(StatementImportRow row, CategorySuggestion? suggestion, OwnTransferDetector ownTransfers)
        {
            if (suggestion?.Source is SuggestionSource.Rule or SuggestionSource.History)
            {
                row.ApplySuggestion(suggestion);
                return;
            }

            var movement = InvestmentDetector.Detect(row.OriginalDescription, row.SignedAmount);
            if (movement is not null)
            {
                var similarInvestment = movement == InvestmentMovement.Application
                    && suggestion is { Type: MoneyFlowType.Investment, Action: RuleAction.Categorize };

                row.ApplyInvestmentDetection(movement.Value, similarInvestment ? suggestion!.CategoryId : null);
                return;
            }

            if (ownTransfers.IsOwnTransfer(row.OriginalDescription))
            {
                row.ApplyOwnTransferDetection();
                return;
            }

            if (suggestion is not null)
                row.ApplySuggestion(suggestion);
        }
    }
}
