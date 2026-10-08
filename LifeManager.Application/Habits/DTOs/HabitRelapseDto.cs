namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>Body of <c>POST /api/Habits/{id}/Relapses</c>.</summary>
    /// <param name="Date">"yyyy-MM-dd": today or yesterday.</param>
    public record HabitRelapseDto(DateOnly Date);

    /// <summary>What a relapse or its undo changed: the habit's day and streak, and the player's profile.</summary>
    /// <param name="Relapsed">Whether <paramref name="Date"/> has a relapse now (false after an undo).</param>
    /// <param name="WeekRelapseCount">Weekly limit only: the week's relapses now.</param>
    public record HabitRelapseResultDto(
        int HabitId,
        DateOnly Date,
        bool Relapsed,
        int CurrentStreak,
        int LongestStreak,
        WalletChangeDto Wallet,
        int? WeekRelapseCount);
}
