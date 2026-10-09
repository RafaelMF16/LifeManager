using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Habits
{
    public class GameLedgerRepository(LifeManagerDbContext dbContext) : IGameLedgerRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<PagedList<GameLedgerEntry>> GetPagedByUserIdAsync(UserId userId, PageRequest pageRequest, HabitId? habitId, CancellationToken cancellationToken)
        {
            var query = _dbContext.GameLedgerEntries
                .AsNoTracking()
                .Where(entry => entry.UserId == userId);

            if (habitId is not null)
                query = query.Where(entry => entry.HabitId == habitId);

            return await query
                .OrderByDescending(entry => entry.CreatedAt)
                .ThenByDescending(entry => entry.Id)
                .ToPagedListAsync(pageRequest, cancellationToken);
        }

        public async Task<HabitEarnings> GetHabitEarningsAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            var earningKinds = GameLedgerEntry.HabitEarningKinds.ToArray();

            var entries = _dbContext.GameLedgerEntries
                .AsNoTracking()
                .Where(entry => entry.UserId == userId
                    && entry.OccurredOn >= from
                    && entry.OccurredOn <= to
                    && (earningKinds.Contains(entry.Kind) || (entry.Kind == GameLedgerEntryKind.Undo && entry.HabitId != null)));

            var coins = await entries.SumAsync(entry => entry.CoinsDelta, cancellationToken);
            var firstDay = await entries
                .Where(entry => entry.CoinsDelta > 0)
                .MinAsync(entry => (DateOnly?)entry.OccurredOn, cancellationToken);

            return new HabitEarnings(coins, firstDay);
        }
    }
}
