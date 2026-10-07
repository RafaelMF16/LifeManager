using LifeManager.Domain.RecurringTransactions;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class RecurringTransactionSingleton : List<RecurringTransaction>
    {
        private RecurringTransactionSingleton() { }

        private static readonly Lazy<RecurringTransactionSingleton> lazy = new(() => new RecurringTransactionSingleton());

        public static RecurringTransactionSingleton Instance => lazy.Value;
    }
}
