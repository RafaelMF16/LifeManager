namespace LifeManager.Domain.Users
{
    public class User
    {
        public UserId Id { get; }
        public UserName Name { get; private set; }
        public Email Email { get; private set; }
        public UserPassword Password { get; private set; }
        public DateTimeOffset CreationDate { get; } = DateTimeOffset.UtcNow;

        private User(
            UserId id,
            UserName name,
            Email email,
            UserPassword password)
        {
            Id = id;
            Name = name;
            Email = email;
            Password = password;
        }

        public static User Create(
            int id,
            string name,
            string email,
            string password)
        {
            var userId = new UserId(id);
            var userName = UserName.Create(name);
            var userEmail = Email.Create(email);
            var userPassword = UserPassword.Create(password);

            return new User(userId, userName, userEmail, userPassword);
        }
    }
}