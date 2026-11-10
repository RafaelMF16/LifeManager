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
        public DateTimeOffset CreationDate { get; private set; }

        private User(
            UserName name,
            Email email,
            PasswordHash passwordHash,
            DateTimeOffset creationDate)
        {
            Name = name;
            Email = email;
            PasswordHash = passwordHash;
            CreationDate = creationDate;
        }

        /// <summary><paramref name="creationDate"/> comes from the caller's clock: the domain never reads one.</summary>
        public static Result<User> Create(
            string name,
            string email,
            string passwordHash,
            DateTimeOffset creationDate)
        {
            return UserName.Create(name)
                .Bind(userName => Email.Create(email)
                    .Bind(userEmail => PasswordHash.Create(passwordHash)
                        .Map(hash => new User(userName, userEmail, hash, creationDate))));
        }

        public void AssignId(int id)
        {
            Id = new UserId(id);
        }

        /// <summary>The account's display name; trimmed, since only the sign-up form trimmed it before.</summary>
        public Result Rename(string name)
        {
            var nameResult = UserName.Create(name?.Trim() ?? string.Empty);
            if (!nameResult.IsSuccess)
                return nameResult.Error;

            Name = nameResult.Value;
            return Result.Success();
        }

        public void ChangePassword(PasswordHash passwordHash)
        {
            PasswordHash = passwordHash;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not User other)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Id is null || other.Id is null)
                return false;

            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
            => Id?.GetHashCode() ?? base.GetHashCode();
    }
}