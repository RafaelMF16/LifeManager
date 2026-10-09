using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Paging;
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
        /// <summary>The user's statement, newest first; only one habit's entries when <paramref name="habitId"/> is given.</summary>
        Task<PagedList<GameLedgerEntry>> GetPagedByUserIdAsync(UserId userId, PageRequest pageRequest, HabitId? habitId, CancellationToken cancellationToken);

        /// <summary>The user's latest entry of that <paramref name="kind"/>; null when there is none.</summary>
        Task<GameLedgerEntry?> GetLatestAsync(UserId userId, GameLedgerEntryKind kind, CancellationToken cancellationToken);

        Task<HabitEarnings> GetHabitEarningsAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    }
}
