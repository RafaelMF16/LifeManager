using LifeManager.Domain.Habits;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class GameLedgerEntrySingleton : List<GameLedgerEntry>
    {
        private GameLedgerEntrySingleton() { }

        private static readonly Lazy<GameLedgerEntrySingleton> lazy = new(() => new GameLedgerEntrySingleton());

        public static GameLedgerEntrySingleton Instance => lazy.Value;
    }
}
