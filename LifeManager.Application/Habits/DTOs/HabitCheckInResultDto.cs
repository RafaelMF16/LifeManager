namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>What a check-in or its undo changed: the habit's day and streak, and the player's profile.</summary>
    /// <param name="Done">Whether <paramref name="Date"/> is checked in now (false after an undo).</param>
    public record HabitCheckInResultDto(
        int HabitId,
        DateOnly Date,
        bool Done,
        int CurrentStreak,
        int LongestStreak,
        WalletChangeDto Wallet);
}
