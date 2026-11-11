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

        /// <summary>When the user asked to delete the account; null when no deletion is pending.</summary>
        public DateTimeOffset? DeletionRequestedAt { get; private set; }

        /// <summary>When the account is deleted for good, unless the user signs in before (<see cref="AccountDeletion"/>).</summary>
        public DateTimeOffset? DeletionScheduledFor { get; private set; }

        public bool IsDeletionPending => DeletionScheduledFor is not null;

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

        /// <summary>
        /// Schedules the deletion <see cref="AccountDeletion.GracePeriod"/> from <paramref name="now"/>. Idempotent: a
        /// pending deletion keeps its date, so asking again never pushes it back. True when this call scheduled it.
        /// </summary>
        public bool ScheduleDeletion(DateTimeOffset now)
        {
            if (IsDeletionPending)
                return false;

            DeletionRequestedAt = now;
            DeletionScheduledFor = now + AccountDeletion.GracePeriod;
            return true;
        }

        /// <summary>Signing in during the grace period keeps the account.</summary>
        public void CancelDeletion()
        {
            DeletionRequestedAt = null;
            DeletionScheduledFor = null;
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