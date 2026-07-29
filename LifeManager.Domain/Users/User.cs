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

        public static User Create(
            string name,
            string email,
            string password)
        {
            var userName = UserName.Create(name);
            var userEmail = Email.Create(email);
            var userPassword = PasswordHash.Create(password);

            return new User(userName, userEmail, userPassword);
        }

        public void AssignId(int id)
        {
            Id = new UserId(id);
        }
    }
}