using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.AccountHolders.Interfaces
{
    public interface IAccountHolderRepository
    {
        /// <summary>The user's holders in their list's order; empty when there are none.</summary>
        Task<IReadOnlyList<AccountHolder>> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken);

        /// <summary>Replaces every holder of the list's user with the list's, in one database transaction.</summary>
        Task ReplaceAsync(AccountHolderList accountHolderList, CancellationToken cancellationToken);
    }
}
