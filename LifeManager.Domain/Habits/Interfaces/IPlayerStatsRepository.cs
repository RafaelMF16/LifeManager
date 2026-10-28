using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits.Interfaces
{
    /// <summary>Read-only numbers of the player's history for the profile page.</summary>
    public interface IPlayerStatsRepository
    {
        /// <summary>The lifetime numbers, in one round trip.</summary>
        Task<PlayerStats> GetStatsAsync(UserId userId, CancellationToken cancellationToken);

        /// <summary>The ledger's sum before <paramref name="start"/> and its sum per game day up to <paramref name="end"/>.</summary>
        Task<PlayerLedgerHistory> GetLedgerHistoryAsync(UserId userId, DateOnly start, DateOnly end, CancellationToken cancellationToken);
    }
}
