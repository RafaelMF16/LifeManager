using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Habits
{
    public class PlayerStatsRepository(LifeManagerDbContext dbContext) : IPlayerStatsRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        /// <summary>
        /// Every number is a subquery of a single SELECT. The ones shared with the achievements are the same
        /// expressions as AchievementRepository's progress.
        /// </summary>
        public async Task<PlayerStats> GetStatsAsync(UserId userId, CancellationToken cancellationToken)
        {
            var checkIns = _dbContext.HabitCheckIns.Where(checkIn => checkIn.UserId == userId);
            var keptCheckIns = checkIns.Where(checkIn => checkIn.Status == HabitCheckInStatus.Done || checkIn.Status == HabitCheckInStatus.Clean);
            var ledger = _dbContext.GameLedgerEntries.Where(entry => entry.UserId == userId);
            var earningKinds = GameLedgerEntry.HabitEarningKinds.ToArray();

            var row = await _dbContext.Users
                .Where(user => user.Id == userId)
                .Select(user => new
                {
                    CheckIns = checkIns.Count(checkIn => checkIn.Status == HabitCheckInStatus.Done),
                    CleanDays = checkIns.Count(checkIn => checkIn.Status == HabitCheckInStatus.Clean),
                    LongestStreakDays = _dbContext.Habits
                        .Where(habit => habit.UserId == userId)
                        .Max(habit => (int?)(habit.FrequencyType == HabitFrequencyType.TimesPerWeek
                            ? habit.LongestStreak * HabitRewards.DaysPerStreakWeek
                            : habit.LongestStreak)),
                    MostKeptDaysInOneHabit = keptCheckIns
                        .GroupBy(checkIn => checkIn.HabitId)
                        .Select(days => (int?)days.Count())
                        .Max(),
                    ActiveDays = keptCheckIns.Select(checkIn => checkIn.Date).Distinct().Count(),
                    HabitsCreated = _dbContext.Habits.Count(habit => habit.UserId == userId),
                    Redemptions = _dbContext.RewardRedemptions.Count(redemption => redemption.UserId == userId && redemption.UndoneAt == null),
                    CoinsEarned = ledger
                        .Where(entry => earningKinds.Contains(entry.Kind)
                            || entry.Kind == GameLedgerEntryKind.AchievementUnlocked
                            || (entry.Kind == GameLedgerEntryKind.Undo && entry.HabitId != null))
                        .Sum(entry => (int?)entry.CoinsDelta),
                    CoinsSpent = ledger
                        .Where(entry => entry.Kind == GameLedgerEntryKind.RewardRedeemed
                            || entry.Kind == GameLedgerEntryKind.ItemPurchased
                            || (entry.Kind == GameLedgerEntryKind.Undo && (entry.RewardId != null || entry.ItemKind != null)))
                        .Sum(entry => (int?)-entry.CoinsDelta),
                    Knockouts = ledger.Count(entry => entry.Kind == GameLedgerEntryKind.Knockout),
                    AchievementsUnlocked = _dbContext.AchievementUnlocks.Count(unlock => unlock.UserId == userId),
                    PlayingSince = ledger.Min(entry => (DateOnly?)entry.OccurredOn)
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (row is null)
                return new PlayerStats(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null);

            return new PlayerStats(
                row.CheckIns,
                row.CleanDays,
                row.LongestStreakDays ?? 0,
                row.MostKeptDaysInOneHabit ?? 0,
                row.ActiveDays,
                row.HabitsCreated,
                row.Redemptions,
                Math.Max(row.CoinsEarned ?? 0, 0),
                Math.Max(row.CoinsSpent ?? 0, 0),
                row.Knockouts,
                row.AchievementsUnlocked,
                row.PlayingSince);
        }

        public async Task<PlayerLedgerHistory> GetLedgerHistoryAsync(UserId userId, DateOnly start, DateOnly end, CancellationToken cancellationToken)
        {
            var ledger = _dbContext.GameLedgerEntries.Where(entry => entry.UserId == userId);

            var before = await ledger
                .Where(entry => entry.OccurredOn < start)
                .GroupBy(entry => 1)
                .Select(entries => new PlayerTotals(entries.Sum(entry => entry.XpDelta), entries.Sum(entry => entry.CoinsDelta)))
                .SingleOrDefaultAsync(cancellationToken);

            var days = await ledger
                .Where(entry => entry.OccurredOn >= start && entry.OccurredOn <= end)
                .GroupBy(entry => entry.OccurredOn)
                .Select(entries => new PlayerDayDelta(entries.Key, entries.Sum(entry => entry.XpDelta), entries.Sum(entry => entry.CoinsDelta)))
                .ToListAsync(cancellationToken);

            return new PlayerLedgerHistory(before ?? new PlayerTotals(0, 0), days);
        }
    }
}
