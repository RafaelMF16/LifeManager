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
        Task UpdatePasswordHashAsync(UserId id, PasswordHash passwordHash, CancellationToken cancellationToken);
    }
}
