namespace LifeManager.Domain.Achievements
{
    /// <summary>
    /// The player's whole history in the numbers the achievements count, archived habits included. Read fresh each
    /// time, so achievements count the past too.
    /// </summary>
    /// <param name="CheckIns">Check-ins that exist now (an undone one no longer counts).</param>
    /// <param name="LongestStreakDays">The best streak record of any habit; a weekly habit's weeks count 7 days each.</param>
    /// <param name="Level">The profile's current level.</param>
    /// <param name="Redemptions">Reward redemptions not undone.</param>
    /// <param name="CleanDays">Clean days of habits to avoid.</param>
    /// <param name="KeptDaysAfterFirstKnockout">Days done or clean after the day of the first knockout; 0 without one.</param>
    /// <param name="MostKeptDaysInOneHabit">The most days done or clean of a single habit.</param>
    public record AchievementProgress(
        int CheckIns,
        int LongestStreakDays,
        int Level,
        int Redemptions,
        int CleanDays,
        int KeptDaysAfterFirstKnockout,
        int MostKeptDaysInOneHabit);
}
