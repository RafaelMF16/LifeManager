namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>What a check-in or its undo changed: the habit's day and streak, and the player's profile.</summary>
    /// <param name="Done">Whether <paramref name="Date"/> is checked in now (false after an undo).</param>
    /// <param name="Wallet">Everything applied, the milestone's coins included.</param>
    /// <param name="MilestoneDays">The biggest streak milestone this check-in passed (7, 30, 66, 100 days); null when none.</param>
    /// <param name="MilestoneCoins">Coins those milestones added (already counted in <paramref name="Wallet"/>).</param>
    /// <param name="FreezesEarned">Streak freezes the check-in added to the player's (who holds at most 2).</param>
    public record HabitCheckInResultDto(
        int HabitId,
        DateOnly Date,
        bool Done,
        int CurrentStreak,
        int LongestStreak,
        WalletChangeDto Wallet,
        int? MilestoneDays,
        int MilestoneCoins,
        int FreezesEarned);
}
