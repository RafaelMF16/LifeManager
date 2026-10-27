using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.AccountHolders;
using LifeManager.Domain.AccountHolders.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.AccountHolders.Mocks
{
    public class AccountHolderRepositoryMock : IAccountHolderRepository
    {
        private readonly AccountHolderSingleton _instance;
        private static int _lastId;

        public AccountHolderRepositoryMock()
        {
            _instance = AccountHolderSingleton.Instance;
        }

        public Task<IReadOnlyList<AccountHolder>> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken)
        {
            IReadOnlyList<AccountHolder> holders = [.. _instance
                .Where(accountHolder => accountHolder.UserId == userId)
                .OrderBy(accountHolder => accountHolder.Position)];

            return Task.FromResult(holders);
        }

        public Task ReplaceAsync(AccountHolderList accountHolderList, CancellationToken cancellationToken)
        {
            _instance.RemoveAll(accountHolder => accountHolder.UserId == accountHolderList.UserId);

            foreach (var accountHolder in accountHolderList.Holders)
            {
                accountHolder.AssignId(Interlocked.Increment(ref _lastId));
                _instance.Add(accountHolder);
            }

            return Task.CompletedTask;
        }
    }
}
