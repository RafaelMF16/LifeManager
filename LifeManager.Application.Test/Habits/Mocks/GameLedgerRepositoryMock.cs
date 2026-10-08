using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Habits.Mocks
{
    public class GameLedgerRepositoryMock : IGameLedgerRepository
    {
        private readonly GameLedgerEntrySingleton _ledger = GameLedgerEntrySingleton.Instance;

        public Task<HabitEarnings> GetHabitEarningsAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            var entries = _ledger
                .Where(entry => entry.UserId == userId
                    && entry.OccurredOn >= from
                    && entry.OccurredOn <= to
                    && (GameLedgerEntry.HabitEarningKinds.Contains(entry.Kind) || (entry.Kind == GameLedgerEntryKind.Undo && entry.HabitId is not null)))
                .ToList();

            var firstDay = entries.Where(entry => entry.CoinsDelta > 0).Select(entry => (DateOnly?)entry.OccurredOn).Min();

            return Task.FromResult(new HabitEarnings(entries.Sum(entry => entry.CoinsDelta), firstDay));
        }
    }
}
