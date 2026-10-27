using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Users.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken);
        Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken);
    }
}
