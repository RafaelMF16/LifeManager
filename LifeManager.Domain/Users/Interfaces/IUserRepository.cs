using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Users.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken);
        Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken);
        Task UpdateNameAsync(UserId id, UserName name, CancellationToken cancellationToken);
        /// <summary>
        /// Stores the new password and, in the same database transaction, revokes every active session of the user but
        /// <paramref name="keepTokenHash"/> (the one changing it; all of them when null).
        /// </summary>
        Task ChangePasswordAsync(UserId id, PasswordHash passwordHash, RefreshTokenHash? keepTokenHash, CancellationToken cancellationToken);
        /// <summary>
        /// Marks the deletion only while none is pending (so a repeated request keeps the first date) and, in the same
        /// database transaction, revokes every active session of the user. True when this call marked it.
        /// </summary>
        Task<bool> ScheduleDeletionAsync(UserId id, DateTimeOffset requestedAt, DateTimeOffset scheduledFor, CancellationToken cancellationToken);

        /// <summary>Clears a pending deletion (a sign-in during the grace period). False when none was pending or the user is gone.</summary>
        Task<bool> CancelDeletionAsync(UserId id, CancellationToken cancellationToken);

        Task UpdatePasswordHashAsync(UserId id, PasswordHash passwordHash, CancellationToken cancellationToken);
    }
}
