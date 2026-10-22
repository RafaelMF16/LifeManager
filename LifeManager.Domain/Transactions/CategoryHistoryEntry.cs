using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Domain.Transactions
{
    /// <summary>The user's transactions grouped by description, type and category: the history category suggestions learn from.</summary>
    public record CategoryHistoryEntry(
        string NormalizedDescription,
        MoneyFlowType Type,
        CategoryId CategoryId,
        int Count,
        DateOnly LastDate);
}
