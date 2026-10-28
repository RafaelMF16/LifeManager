namespace LifeManager.Domain.Habits
{
    /// <summary>
    /// The player's lifetime numbers for the profile page, archived habits included. The ones the achievements also
    /// count use the same rules as <see cref="Achievements.AchievementProgress"/>.
    /// </summary>
    /// <param name="CheckIns">Done check-ins that exist now.</param>
    /// <param name="CleanDays">Clean days of habits to avoid.</param>
    /// <param name="LongestStreakDays">The best streak record of any habit; a weekly habit's weeks count 7 days each.</param>
    /// <param name="MostKeptDaysInOneHabit">The most days done or clean of a single habit.</param>
    /// <param name="ActiveDays">Distinct days with at least one check-in done or clean.</param>
    /// <param name="HabitsCreated">Every habit the player created, archived ones included.</param>
    /// <param name="Redemptions">Reward redemptions not undone.</param>
    /// <param name="CoinsEarned">Coins earned with habits (the shop's earning pace rule: undone check-ins take theirs back) plus achievement bonuses.</param>
    /// <param name="CoinsSpent">Coins spent on rewards and game items, minus what undoing them gave back.</param>
    /// <param name="Knockouts">Times the player was knocked out.</param>
    /// <param name="AchievementsUnlocked">Achievements unlocked.</param>
    /// <param name="PlayingSince">The game day of the first ledger entry; null before any.</param>
    public record PlayerStats(
        int CheckIns,
        int CleanDays,
        int LongestStreakDays,
        int MostKeptDaysInOneHabit,
        int ActiveDays,
        int HabitsCreated,
        int Redemptions,
        int CoinsEarned,
        int CoinsSpent,
        int Knockouts,
        int AchievementsUnlocked,
        DateOnly? PlayingSince);
}
