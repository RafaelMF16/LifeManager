using LifeManager.Domain.AccountHolders;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class AccountHolderSingleton : List<AccountHolder>
    {
        private AccountHolderSingleton() { }

        private static readonly Lazy<AccountHolderSingleton> lazy = new(() => new AccountHolderSingleton());

        public static AccountHolderSingleton Instance => lazy.Value;
    }
}
