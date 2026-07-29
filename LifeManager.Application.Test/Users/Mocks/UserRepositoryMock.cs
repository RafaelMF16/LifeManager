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

        public User Add(User user)
        {
            _instance.Add(user);

            var newId = _instance.Count;
            user.AssignId(newId);

            return user;
        }

        public User? GetUserByEmail(string email)
        {
            return _instance.FirstOrDefault(user => user.Email.Value == email);
        }
    }
}