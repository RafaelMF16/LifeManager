using LifeManager.Domain.AccountHolders.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.AccountHolders
{
    /// <summary>One name of the user's <see cref="AccountHolderList"/>, created only through it.</summary>
    public class AccountHolder
    {
        public AccountHolderId? Id { get; private set; }
        public UserId UserId { get; }
        public AccountHolderName Name { get; }

        /// <summary>The name's place in the list, so it comes back in the order the user wrote it.</summary>
        public int Position { get; }

        private AccountHolder(UserId userId, AccountHolderName name, int position)
        {
            UserId = userId;
            Name = name;
            Position = position;
        }

        internal static AccountHolder For(UserId userId, AccountHolderName name, int position) => new(userId, name, position);

        public void AssignId(int id)
        {
            Id = new AccountHolderId(id);
        }
    }
}
