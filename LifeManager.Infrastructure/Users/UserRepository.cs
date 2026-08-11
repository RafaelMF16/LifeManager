using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Interfaces;

namespace LifeManager.Infrastructure.Users
{
    public class UserRepository : IUserRepository
    {
        public User Add(User user)
        {
            throw new NotImplementedException();
        }

        public User? GetUserByEmail(string email)
        {
            throw new NotImplementedException();
        }
    }
}