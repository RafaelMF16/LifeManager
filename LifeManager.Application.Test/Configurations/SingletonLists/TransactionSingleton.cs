using LifeManager.Domain.Transactions;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class TransactionSingleton : List<Transaction>
    {
        private TransactionSingleton() { }

        private static readonly Lazy<TransactionSingleton> lazy = new(() => new TransactionSingleton());

        public static TransactionSingleton Instance => lazy.Value;
    }
}
