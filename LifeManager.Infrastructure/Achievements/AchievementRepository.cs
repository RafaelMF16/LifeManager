using LifeManager.Domain.Achievements;
using LifeManager.Domain.Achievements.Interfaces;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Habits;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Achievements
{
    public class AchievementRepository(LifeManagerDbContext dbContext) : IAchievementRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public Task<AchievementProgress> GetProgressAsync(UserId userId, CancellationToken cancellationToken)
            => ReadProgressAsync(userId, cancellationToken);

        public async Task<IReadOnlyList<AchievementUnlock>> GetUnlocksAsync(UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.AchievementUnlocks
                .AsNoTracking()
                .Where(unlock => unlock.UserId == userId)
                .OrderBy(unlock => unlock.UnlockedAt)
                .ThenBy(unlock => unlock.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<AchievementUnlock>> UnlockAsync(
            UserId userId,
            Func<PlayerProfile, AchievementProgress, IReadOnlyCollection<AchievementUnlock>, AchievementUnlockDecision?> decide,
            CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var profile = await PlayerWallet.LockAsync(_dbContext, userId, cancellationToken);

            // Under the lock: a sync racing this one waits here, then finds these unlocks and pays nothing.
            var progress = await ReadProgressAsync(userId, cancellationToken);
            var unlocked = await GetUnlocksAsync(userId, cancellationToken);

            var decision = decide(profile, progress, unlocked);
            if (decision is null)
                return [];

            _dbContext.AchievementUnlocks.AddRange(decision.Unlocks);
            await PlayerWallet.SaveAsync(_dbContext, profile, decision.Entries, cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return decision.Unlocks;
        }

        public async Task MarkSeenAsync(UserId userId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            await _dbContext.AchievementUnlocks
                .Where(unlock => unlock.UserId == userId && unlock.SeenAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(unlock => unlock.SeenAt, now), cancellationToken);
        }

        /// <summary>Every number in one round trip: each one is a subquery of a single SELECT.</summary>
        private async Task<AchievementProgress> ReadProgressAsync(UserId userId, CancellationToken cancellationToken)
        {
            var checkIns = _dbContext.HabitCheckIns.Where(checkIn => checkIn.UserId == userId);
            var keptCheckIns = checkIns.Where(checkIn => checkIn.Status == HabitCheckInStatus.Done || checkIn.Status == HabitCheckInStatus.Clean);

            var row = await _dbContext.Users
                .Where(user => user.Id == userId)
                .Select(user => new
                {
                    CheckIns = checkIns.Count(checkIn => checkIn.Status == HabitCheckInStatus.Done),
                    LongestStreakDays = _dbContext.Habits
                        .Where(habit => habit.UserId == userId)
                        .Max(habit => (int?)(habit.FrequencyType == HabitFrequencyType.TimesPerWeek
                            ? habit.LongestStreak * HabitRewards.DaysPerStreakWeek
                            : habit.LongestStreak)),
                    Level = _dbContext.PlayerProfiles
                        .Where(profile => profile.UserId == userId)
                        .Max(profile => (int?)profile.Level),
                    Redemptions = _dbContext.RewardRedemptions.Count(redemption => redemption.UserId == userId && redemption.UndoneAt == null),
                    CleanDays = checkIns.Count(checkIn => checkIn.Status == HabitCheckInStatus.Clean),
                    KeptDaysAfterFirstKnockout = keptCheckIns.Count(checkIn => checkIn.Date > _dbContext.GameLedgerEntries
                        .Where(entry => entry.UserId == userId && entry.Kind == GameLedgerEntryKind.Knockout)
                        .Min(entry => (DateOnly?)entry.OccurredOn)),
                    MostKeptDaysInOneHabit = keptCheckIns
                        .GroupBy(checkIn => checkIn.HabitId)
                        .Select(days => (int?)days.Count())
                        .Max()
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (row is null)
                return new AchievementProgress(0, 0, GameRules.StartingLevel, 0, 0, 0, 0);

            return new AchievementProgress(
                row.CheckIns,
                row.LongestStreakDays ?? 0,
                row.Level ?? GameRules.StartingLevel,
                row.Redemptions,
                row.CleanDays,
                row.KeptDaysAfterFirstKnockout,
                row.MostKeptDaysInOneHabit ?? 0);
        }
    }
}
