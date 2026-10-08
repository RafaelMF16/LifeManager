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

        /// <summary>Several applications in a row read as one: deltas and effects added up.</summary>
        public static GameOutcome Combine(IReadOnlyCollection<GameOutcome> outcomes)
            => new(
                new GameDelta(
                    outcomes.Sum(outcome => outcome.Applied.Coins),
                    outcomes.Sum(outcome => outcome.Applied.Xp),
                    outcomes.Sum(outcome => outcome.Applied.Hp)),
                outcomes.Sum(outcome => outcome.LevelsGained),
                outcomes.Sum(outcome => outcome.LevelUpHpRestored),
                outcomes.Any(outcome => outcome.KnockedOut),
                outcomes.Sum(outcome => outcome.KnockoutCoinsLost),
                outcomes.Sum(outcome => outcome.KnockoutHpRestored));
    }
}
