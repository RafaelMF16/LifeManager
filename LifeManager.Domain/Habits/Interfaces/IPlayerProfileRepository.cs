using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits.Interfaces
{
    public interface IPlayerProfileRepository
    {
        Task<PlayerProfile?> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken);

        /// <summary>
        /// The only way to change a profile. In one database transaction: creates the user's profile if it doesn't
        /// exist yet, locks it, calls <paramref name="apply"/> (which changes the profile and returns the ledger entries
        /// describing the change), saves the entries and the profile, and commits. Concurrent calls for the same user
        /// run one after the other.
        /// </summary>
        Task<PlayerProfile> ApplyAsync(UserId userId, Func<PlayerProfile, IReadOnlyList<GameLedgerEntry>> apply, CancellationToken cancellationToken);

        /// <summary>
        /// Stores the avatar and the title, creating the user's profile first if it doesn't exist yet. Writes only those
        /// two columns, without the lock: they never race with <see cref="ApplyAsync"/>, which doesn't write them.
        /// </summary>
        Task UpdateAppearanceAsync(UserId userId, PlayerAvatar avatar, AchievementCode? title, CancellationToken cancellationToken);
    }
}
