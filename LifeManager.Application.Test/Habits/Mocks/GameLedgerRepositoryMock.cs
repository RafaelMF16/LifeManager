using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Habits.Mocks
{
    public class GameLedgerRepositoryMock : IGameLedgerRepository
    {
        private readonly GameLedgerEntrySingleton _ledger = GameLedgerEntrySingleton.Instance;

        public Task<PagedList<GameLedgerEntry>> GetPagedByUserIdAsync(UserId userId, PageRequest pageRequest, HabitId? habitId, CancellationToken cancellationToken)
        {
            var matching = _ledger
                .Where(entry => entry.UserId == userId && (habitId is null || entry.HabitId == habitId))
                .OrderByDescending(entry => entry.CreatedAt)
                .ThenByDescending(entry => entry.Id!.Value)
                .ToList();
            IReadOnlyList<GameLedgerEntry> items = [.. matching.Skip(pageRequest.Skip).Take(pageRequest.PageSize)];

            return Task.FromResult(new PagedList<GameLedgerEntry>(items, matching.Count, pageRequest.Page, pageRequest.PageSize));
        }

        public Task<GameLedgerEntry?> GetLatestAsync(UserId userId, GameLedgerEntryKind kind, CancellationToken cancellationToken)
            => Task.FromResult(_ledger
                .Where(entry => entry.UserId == userId && entry.Kind == kind)
                .OrderByDescending(entry => entry.CreatedAt)
                .ThenByDescending(entry => entry.Id!.Value)
                .FirstOrDefault());

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
