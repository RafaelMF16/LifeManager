using LifeManager.Domain.Habits;

namespace LifeManager.Application.Habits.DTOs
{
    /// <summary><c>GET /api/Habits/Profile/Stats</c>: the lifetime numbers (see <see cref="PlayerStats"/>).</summary>
    /// <param name="PlayingSince"><c>yyyy-MM-dd</c>; null before the first ledger entry.</param>
    public record PlayerStatsDto(
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
        DateOnly? PlayingSince)
    {
        public static PlayerStatsDto From(PlayerStats stats)
            => new(
                stats.CheckIns,
                stats.CleanDays,
                stats.LongestStreakDays,
                stats.MostKeptDaysInOneHabit,
                stats.ActiveDays,
                stats.HabitsCreated,
                stats.Redemptions,
                stats.CoinsEarned,
                stats.CoinsSpent,
                stats.Knockouts,
                stats.AchievementsUnlocked,
                stats.PlayingSince);
    }
}
