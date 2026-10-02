using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Users.Mocks
{
    public class UserRepositoryMock : IUserRepository
    {
        private readonly UserSingleton _instance;

        public UserRepositoryMock()
        {
            _instance = UserSingleton.Instance;
        }

        public Task<User> AddAsync(User user, CancellationToken cancellationToken)
        {
            _instance.Add(user);

            var newId = _instance.Count;
            user.AssignId(newId);

            return Task.FromResult(user);
        }

        public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken)
            => Task.FromResult(_instance.SingleOrDefault(user => user.Id == id));

        public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken)
            => Task.FromResult(_instance.SingleOrDefault(user => user.Email.Value == email.Value));

        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken)
            => Task.FromResult(_instance.Any(user => user.Email.Value == email.Value));
    }
}
