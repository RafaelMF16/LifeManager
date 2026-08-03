using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Users
{
    public class User
    {
        public UserId? Id { get; private set; }
        public UserName Name { get; private set; }
        public Email Email { get; private set; }
        public PasswordHash PasswordHash { get; private set; }
        public DateTimeOffset CreationDate { get; } = DateTimeOffset.UtcNow;

        private User(
            UserName name,
            Email email,
            PasswordHash password)
        {
            Name = name;
            Email = email;
            PasswordHash = password;
        }

        public static Result<User> Create(
            string name,
            string email,
            string password)
        {
            var userNameResult = UserName.Create(name);
            if (!userNameResult.IsSuccess)
                return userNameResult.Error;

            var userEmailResult = Email.Create(email);
            if (!userEmailResult.IsSuccess)
                return userEmailResult.Error;

            var userPasswordResult = PasswordHash.Create(password);
            if (!userPasswordResult.IsSuccess)
                return userPasswordResult.Error;

            return new User(userNameResult.Value, userEmailResult.Value, userPasswordResult.Value);
        }

        public void AssignId(int id)
        {
            Id = new UserId(id);
        }
    }
}