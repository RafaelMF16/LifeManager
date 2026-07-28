using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Users
{
    public class User
    {
        public UserId? Id { get; }
        public UserName Name { get; private set; }
        public Email Email { get; private set; }
        public UserPassword Password { get; private set; }
        public DateTimeOffset CreationDate { get; } = DateTimeOffset.UtcNow;

        private User(
            UserName name,
            Email email,
            UserPassword password)
        {
            Name = name;
            Email = email;
            Password = password;
        }

        public static User Create(
            string name,
            string email,
            string password)
        {
            var userName = UserName.Create(name);
            var userEmail = Email.Create(email);
            var userPassword = UserPassword.Create(password);

            return new User(userName, userEmail, userPassword);
        }
    }
}