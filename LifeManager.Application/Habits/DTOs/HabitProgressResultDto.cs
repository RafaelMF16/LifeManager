namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>What logging or removing progress changed: the day's total and entries, and the check-in it completed or undid.</summary>
    /// <param name="Progress">The day's total after the change.</param>
    /// <param name="Done">Whether <paramref name="Date"/> is checked in now.</param>
    /// <param name="Entries">The day's entries after the change, oldest first.</param>
    /// <param name="Wallet">What completing or undoing the day applied; null when the check-in didn't change.</param>
    /// <param name="MilestoneDays">The biggest streak milestone a completion passed (7, 30, 66, 100 days); null when none.</param>
    /// <param name="MilestoneCoins">Coins those milestones added (already counted in <paramref name="Wallet"/>).</param>
    /// <param name="FreezesEarned">Streak freezes a completion added to the player's.</param>
    public record HabitProgressResultDto(
        int HabitId,
        DateOnly Date,
        int Progress,
        bool Done,
        IReadOnlyList<HabitProgressEntryDto> Entries,
        int CurrentStreak,
        int LongestStreak,
        WalletChangeDto? Wallet,
        int? MilestoneDays,
        int MilestoneCoins,
        int FreezesEarned);
}
