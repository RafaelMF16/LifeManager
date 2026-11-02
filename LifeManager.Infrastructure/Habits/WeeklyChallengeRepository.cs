using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Challenges;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LifeManager.Infrastructure.Habits
{
    public class WeeklyChallengeRepository(LifeManagerDbContext dbContext) : IWeeklyChallengeRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<IReadOnlyList<WeeklyChallenge>> GetByWeekAsync(UserId userId, DateOnly weekStart, CancellationToken cancellationToken)
            => await _dbContext.WeeklyChallenges
                .AsNoTracking()
                .Where(challenge => challenge.UserId == userId && challenge.WeekStart == weekStart)
                .OrderBy(challenge => challenge.CreatedAt)
                .ThenBy(challenge => challenge.Id)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<WeeklyChallenge>> GetActiveAsync(UserId userId, CancellationToken cancellationToken)
            => await _dbContext.WeeklyChallenges
                .AsNoTracking()
                .Where(challenge => challenge.UserId == userId && challenge.Status == ChallengeStatus.Active)
                .OrderBy(challenge => challenge.WeekStart)
                .ThenBy(challenge => challenge.Id)
                .ToListAsync(cancellationToken);

        public Task<WeeklyChallenge?> GetByIdAsync(WeeklyChallengeId id, UserId userId, CancellationToken cancellationToken)
            => _dbContext.WeeklyChallenges
                .AsNoTracking()
                .SingleOrDefaultAsync(challenge => challenge.Id == id && challenge.UserId == userId, cancellationToken);

        public Task<PagedList<WeeklyChallenge>> GetHistoryAsync(UserId userId, DateOnly beforeWeek, PageRequest pageRequest, CancellationToken cancellationToken)
            => _dbContext.WeeklyChallenges
                .AsNoTracking()
                .Where(challenge => challenge.UserId == userId
                    && challenge.WeekStart < beforeWeek
                    && challenge.Status != ChallengeStatus.Cancelled)
                .OrderByDescending(challenge => challenge.WeekStart)
                .ThenBy(challenge => challenge.CreatedAt)
                .ThenBy(challenge => challenge.Id)
                .ToPagedListAsync(pageRequest, cancellationToken);

        public async Task<IReadOnlyList<WeeklyChallenge>> GetUnseenCompletedAsync(UserId userId, CancellationToken cancellationToken)
            => await _dbContext.WeeklyChallenges
                .AsNoTracking()
                .Where(challenge => challenge.UserId == userId && challenge.Status == ChallengeStatus.Completed && challenge.SeenAt == null)
                .OrderBy(challenge => challenge.CompletedAt)
                .ThenBy(challenge => challenge.Id)
                .ToListAsync(cancellationToken);

        public async Task<Result<WeeklyChallengeId>> AddAsync(WeeklyChallenge challenge, CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // The player's lock: two requests at once count the week one after the other.
            await PlayerWallet.LockAsync(_dbContext, challenge.UserId, cancellationToken);

            var week = await _dbContext.WeeklyChallenges
                .AsNoTracking()
                .Where(stored => stored.UserId == challenge.UserId
                    && stored.WeekStart == challenge.WeekStart
                    && stored.Status != ChallengeStatus.Cancelled)
                .Select(stored => stored.HabitId)
                .ToListAsync(cancellationToken);
            if (week.Contains(challenge.HabitId))
                return ChallengeErrors.AlreadyExists;
            if (week.Count >= GameRules.MaxChallengesPerWeek)
                return ChallengeErrors.WeekLimitReached;

            _dbContext.WeeklyChallenges.Add(challenge);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return ChallengeErrors.AlreadyExists;
            }

            await databaseTransaction.CommitAsync(cancellationToken);

            return challenge.Id!;
        }

        public async Task<bool> DeleteActiveAsync(WeeklyChallengeId id, UserId userId, CancellationToken cancellationToken)
        {
            var deleted = await _dbContext.WeeklyChallenges
                .Where(challenge => challenge.Id == id && challenge.UserId == userId && challenge.Status == ChallengeStatus.Active)
                .ExecuteDeleteAsync(cancellationToken);

            return deleted > 0;
        }

        public async Task<IReadOnlyList<WeeklyChallenge>> CompleteAsync(
            UserId userId,
            IReadOnlyCollection<(WeeklyChallenge Challenge, int Progress)> challenges,
            DateTimeOffset completedAt,
            Func<PlayerProfile, WeeklyChallenge, IReadOnlyList<GameLedgerEntry>> pay,
            CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var profile = await PlayerWallet.LockAsync(_dbContext, userId, cancellationToken);

            var completed = new List<WeeklyChallenge>();
            var entries = new List<GameLedgerEntry>();
            foreach (var (challenge, progress) in challenges)
            {
                // Only if it is still active: a sync racing this one waits on the lock, then finds it completed and pays nothing.
                var moved = await _dbContext.WeeklyChallenges
                    .Where(stored => stored.Id == challenge.Id && stored.UserId == userId && stored.Status == ChallengeStatus.Active)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(stored => stored.Status, ChallengeStatus.Completed)
                        .SetProperty(stored => stored.CompletedAt, completedAt)
                        .SetProperty(stored => stored.FinalProgress, progress), cancellationToken);
                if (moved == 0)
                    continue;

                challenge.Complete(completedAt, progress);
                completed.Add(challenge);
                entries.AddRange(pay(profile, challenge));
            }

            if (completed.Count > 0)
                await PlayerWallet.SaveAsync(_dbContext, profile, entries, cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return completed;
        }

        public async Task FailAsync(IReadOnlyCollection<(WeeklyChallengeId Id, int Progress)> challenges, CancellationToken cancellationToken)
        {
            // One update per challenge (each its own progress); a sync fails at most the few of the weeks that just ended.
            foreach (var (id, progress) in challenges)
                await _dbContext.WeeklyChallenges
                    .Where(challenge => challenge.Id == id && challenge.Status == ChallengeStatus.Active)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(challenge => challenge.Status, ChallengeStatus.Failed)
                        .SetProperty(challenge => challenge.FinalProgress, progress), cancellationToken);
        }

        public async Task CancelAsync(IReadOnlyCollection<WeeklyChallengeId> ids, CancellationToken cancellationToken)
        {
            if (ids.Count == 0)
                return;

            await _dbContext.WeeklyChallenges
                .Where(challenge => ids.Contains(challenge.Id!) && challenge.Status == ChallengeStatus.Active)
                .ExecuteUpdateAsync(setters => setters.SetProperty(challenge => challenge.Status, ChallengeStatus.Cancelled), cancellationToken);
        }

        public async Task MarkSeenAsync(UserId userId, IReadOnlyCollection<WeeklyChallengeId> ids, DateTimeOffset now, CancellationToken cancellationToken)
        {
            await _dbContext.WeeklyChallenges
                .Where(challenge => challenge.UserId == userId && challenge.SeenAt == null && ids.Contains(challenge.Id!))
                .ExecuteUpdateAsync(setters => setters.SetProperty(challenge => challenge.SeenAt, now), cancellationToken);
        }
    }
}
