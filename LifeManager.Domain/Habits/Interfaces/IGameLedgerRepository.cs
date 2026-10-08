using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits.Interfaces
{
    /// <summary>Reads over the player's statement; it is only written through <see cref="IPlayerProfileRepository"/>.</summary>
    public interface IGameLedgerRepository
    {
        /// <summary>
        /// Coins earned with habits from <paramref name="from"/> to <paramref name="to"/> (game days, inclusive): check-ins,
        /// clean days and streak milestones, minus their undos. Redemptions, knockouts and their undos don't count.
        /// </summary>
        Task<HabitEarnings> GetHabitEarningsAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    }
}
