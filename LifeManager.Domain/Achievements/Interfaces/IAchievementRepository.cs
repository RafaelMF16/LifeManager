using LifeManager.Domain.Habits;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Achievements.Interfaces
{
    /// <remarks>
    /// Unlocking locks the user's profile first (the same lock as every other change to the game) and reads the
    /// progress and the unlocks again under it, so two syncs at once can't pay the same achievement twice.
    /// </remarks>
    public interface IAchievementRepository
    {
        /// <summary>The user's history in the numbers the achievements count (see <see cref="AchievementProgress"/>).</summary>
        Task<AchievementProgress> GetProgressAsync(UserId userId, CancellationToken cancellationToken);

        /// <summary>Every achievement the user unlocked, oldest first.</summary>
        Task<IReadOnlyList<AchievementUnlock>> GetUnlocksAsync(UserId userId, CancellationToken cancellationToken);

        /// <summary>
        /// In one database transaction: locks the profile, reads the progress and the unlocks under the lock and calls
        /// <paramref name="decide"/> with the three. It pays the bonuses and returns what to store, or null when there
        /// is nothing new. Then stores the unlocks, the entries and the profile.
        /// </summary>
        /// <returns>The unlocks stored; empty, with nothing written, when <paramref name="decide"/> returned null.</returns>
        Task<IReadOnlyList<AchievementUnlock>> UnlockAsync(
            UserId userId,
            Func<PlayerProfile, AchievementProgress, IReadOnlyCollection<AchievementUnlock>, AchievementUnlockDecision?> decide,
            CancellationToken cancellationToken);

        /// <summary>Marks every unseen unlock of the user as seen at <paramref name="now"/>.</summary>
        Task MarkSeenAsync(UserId userId, DateTimeOffset now, CancellationToken cancellationToken);
    }
}
