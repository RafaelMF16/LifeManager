namespace LifeManager.Domain.Habits
{
    /// <summary>
    /// What <see cref="PlayerProfile.Apply"/> actually did. <see cref="Applied"/> is the requested delta after the limits
    /// (HP between 0 and the max, coins and XP never negative), so undoing it is its exact inverse. Level-up and
    /// knockout effects are kept apart because each one becomes its own ledger entry.
    /// </summary>
    public record GameOutcome(
        GameDelta Applied,
        int LevelsGained,
        int LevelUpHpRestored,
        bool KnockedOut,
        int KnockoutCoinsLost,
        int KnockoutHpRestored)
    {
        public bool LeveledUp => LevelsGained > 0;
    }
}
