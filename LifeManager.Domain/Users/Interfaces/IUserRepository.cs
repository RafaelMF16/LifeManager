using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Users.Interfaces
{
    public interface IUserRepository
    {
        User Add(User user);
        User? GetUserByEmail(Email email);
    }
}
