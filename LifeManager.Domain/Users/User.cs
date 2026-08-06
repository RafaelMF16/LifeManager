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
            string passwordHash)
        {
            return UserName.Create(name)
                .Bind(userName => Email.Create(email)
                    .Bind(userEmail => PasswordHash.Create(passwordHash)
                        .Map(hash => new User(userName, userEmail, hash))));
        }

        public void AssignId(int id)
        {
            Id = new UserId(id);
        }
    }
}