using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits.Interfaces
{
    /// <summary>Read-only numbers of the player's history for the profile page.</summary>
    public interface IPlayerStatsRepository
    {
        /// <summary>The lifetime numbers, in one round trip.</summary>
        Task<PlayerStats> GetStatsAsync(UserId userId, CancellationToken cancellationToken);
    }
}
