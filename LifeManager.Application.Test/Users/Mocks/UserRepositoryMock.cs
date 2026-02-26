using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Interfaces;

namespace LifeManager.Application.Test.Users.Mocks
{
    public class UserRepositoryMock : IUserRepository
    {
        private readonly UserSingleton _instance;

        public UserRepositoryMock()
        {
            _instance = UserSingleton.Instance;
        }

        public void Add(User user)
        {
            _instance.Add(user);
        }

        public User? GetUserByEmail(string email)
        {
            throw new NotImplementedException();
        }
    }
}