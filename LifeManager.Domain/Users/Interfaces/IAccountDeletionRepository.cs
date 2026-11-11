using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Users.Interfaces
{
    /// <summary>The account deletion's final step, run by the hourly job once the grace period is over.</summary>
    public interface IAccountDeletionRepository
    {
        /// <summary>Users whose deletion is due at <paramref name="now"/>, oldest first, at most <paramref name="maxCount"/>.</summary>
        Task<IReadOnlyList<UserId>> GetDueUserIdsAsync(DateTimeOffset now, int maxCount, CancellationToken cancellationToken);

        /// <summary>
        /// In one database transaction: takes the user out of every ranking group (passing an owned group on to its oldest
        /// member, deleting a group left empty), then deletes the user, whose data goes with it through the foreign keys.
        /// False, with nothing written, when the deletion is no longer due (the user signed in and cancelled it).
        /// </summary>
        Task<bool> DeleteIfDueAsync(UserId userId, DateTimeOffset now, CancellationToken cancellationToken);
    }
}
