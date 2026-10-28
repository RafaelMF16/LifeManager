using LifeManager.Domain.Habits;

namespace LifeManager.Domain.Achievements
{
    /// <summary>What a sync stores: the new unlocks and the ledger entries of their bonuses.</summary>
    public record AchievementUnlockDecision(IReadOnlyList<AchievementUnlock> Unlocks, IReadOnlyList<GameLedgerEntry> Entries);
}
