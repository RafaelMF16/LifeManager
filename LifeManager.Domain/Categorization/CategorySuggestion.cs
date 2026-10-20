using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Domain.Categorization
{
    /// <summary>How an imported line should be filed, and how sure the suggestion is.</summary>
    /// <param name="Type">May turn an outgoing line into an <see cref="MoneyFlowType.Investment"/>, when that's what the user did before.</param>
    /// <param name="CategoryId">Null when <paramref name="Action"/> is <see cref="RuleAction.Ignore"/>.</param>
    /// <param name="Uses">How many earlier decisions back the suggestion (shown to the user as "used N times").</param>
    public record CategorySuggestion(
        RuleAction Action,
        MoneyFlowType Type,
        CategoryId? CategoryId,
        SuggestionSource Source,
        SuggestionConfidence Confidence,
        int Uses);
}
