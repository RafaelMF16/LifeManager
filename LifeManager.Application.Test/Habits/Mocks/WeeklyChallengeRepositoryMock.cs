using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Challenges;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Habits.Mocks
{
    /// <summary>
    /// Mirrors WeeklyChallengeRepository over the singletons: the week's limits under the "lock", the compare-and-set
    /// completion that pays once, and detached copies. <see cref="BeforeComplete"/> plays a sync that won the race.
    /// </summary>
    public class WeeklyChallengeRepositoryMock : IWeeklyChallengeRepository
    {
        private readonly WeeklyChallengeSingleton _challenges = WeeklyChallengeSingleton.Instance;
        private readonly PlayerProfileSingleton _profiles = PlayerProfileSingleton.Instance;
        private readonly GameLedgerEntrySingleton _ledger = GameLedgerEntrySingleton.Instance;

        public static Action? BeforeComplete { get; set; }

        public Task<IReadOnlyList<WeeklyChallenge>> GetByWeekAsync(UserId userId, DateOnly weekStart, CancellationToken cancellationToken)
            => Task.FromResult(Copies(_challenges
                .Where(challenge => challenge.UserId == userId && challenge.WeekStart == weekStart)
                .OrderBy(challenge => challenge.CreatedAt)
                .ThenBy(challenge => challenge.Id!.Value)));

        public Task<IReadOnlyList<WeeklyChallenge>> GetActiveAsync(UserId userId, CancellationToken cancellationToken)
            => Task.FromResult(Copies(_challenges
                .Where(challenge => challenge.UserId == userId && challenge.Status == ChallengeStatus.Active)
                .OrderBy(challenge => challenge.WeekStart)
                .ThenBy(challenge => challenge.Id!.Value)));

        public Task<WeeklyChallenge?> GetByIdAsync(WeeklyChallengeId id, UserId userId, CancellationToken cancellationToken)
        {
            var stored = _challenges.SingleOrDefault(challenge => challenge.Id == id && challenge.UserId == userId);

            return Task.FromResult(stored is null ? null : Copy(stored));
        }

        public Task<PagedList<WeeklyChallenge>> GetHistoryAsync(UserId userId, DateOnly beforeWeek, PageRequest pageRequest, CancellationToken cancellationToken)
        {
            var matching = _challenges
                .Where(challenge => challenge.UserId == userId && challenge.WeekStart < beforeWeek && challenge.Status != ChallengeStatus.Cancelled)
                .OrderByDescending(challenge => challenge.WeekStart)
                .ThenBy(challenge => challenge.CreatedAt)
                .ThenBy(challenge => challenge.Id!.Value)
                .ToList();

            return Task.FromResult(new PagedList<WeeklyChallenge>(
                Copies(matching.Skip(pageRequest.Skip).Take(pageRequest.PageSize)), matching.Count, pageRequest.Page, pageRequest.PageSize));
        }

        public Task<IReadOnlyList<WeeklyChallenge>> GetUnseenCompletedAsync(UserId userId, CancellationToken cancellationToken)
            => Task.FromResult(Copies(_challenges
                .Where(challenge => challenge.UserId == userId && challenge.Status == ChallengeStatus.Completed && challenge.SeenAt is null)
                .OrderBy(challenge => challenge.CompletedAt)
                .ThenBy(challenge => challenge.Id!.Value)));

        public Task<Result<WeeklyChallengeId>> AddAsync(WeeklyChallenge challenge, CancellationToken cancellationToken)
        {
            var week = _challenges
                .Where(stored => stored.UserId == challenge.UserId && stored.WeekStart == challenge.WeekStart && stored.Status != ChallengeStatus.Cancelled)
                .ToList();
            if (week.Any(stored => stored.HabitId == challenge.HabitId))
                return Task.FromResult<Result<WeeklyChallengeId>>(ChallengeErrors.AlreadyExists);
            if (week.Count >= GameRules.MaxChallengesPerWeek)
                return Task.FromResult<Result<WeeklyChallengeId>>(ChallengeErrors.WeekLimitReached);

            var id = _challenges.Select(stored => stored.Id!.Value).DefaultIfEmpty(0).Max() + 1;
            challenge.AssignId(id);
            _challenges.Add(Copy(challenge));

            return Task.FromResult<Result<WeeklyChallengeId>>(new WeeklyChallengeId(id));
        }

        public Task<bool> DeleteActiveAsync(WeeklyChallengeId id, UserId userId, CancellationToken cancellationToken)
            => Task.FromResult(_challenges.RemoveAll(challenge =>
                challenge.Id == id && challenge.UserId == userId && challenge.Status == ChallengeStatus.Active) > 0);

        public Task<IReadOnlyList<WeeklyChallenge>> CompleteAsync(
            UserId userId,
            IReadOnlyCollection<(WeeklyChallenge Challenge, int Progress)> challenges,
            DateTimeOffset completedAt,
            Func<PlayerProfile, WeeklyChallenge, IReadOnlyList<GameLedgerEntry>> pay,
            CancellationToken cancellationToken)
        {
            BeforeComplete?.Invoke();
            var profileIndex = LockProfile(userId);
            var profile = CopyProfile(_profiles[profileIndex]);

            var completed = new List<WeeklyChallenge>();
            foreach (var (challenge, progress) in challenges)
            {
                var stored = _challenges.Single(candidate => candidate.Id == challenge.Id);
                if (!stored.Complete(completedAt, progress).IsSuccess)
                    continue;

                challenge.Complete(completedAt, progress);
                completed.Add(challenge);
                foreach (var entry in pay(profile, challenge))
                {
                    entry.AssignId(_ledger.Count + 1);
                    _ledger.Add(entry);
                }
            }

            _profiles[profileIndex] = CopyProfile(profile);

            return Task.FromResult<IReadOnlyList<WeeklyChallenge>>(completed);
        }

        public Task FailAsync(IReadOnlyCollection<(WeeklyChallengeId Id, int Progress)> challenges, CancellationToken cancellationToken)
        {
            foreach (var (id, progress) in challenges)
                _challenges.SingleOrDefault(challenge => challenge.Id == id && challenge.Status == ChallengeStatus.Active)?.Fail(progress);

            return Task.CompletedTask;
        }

        public Task CancelAsync(IReadOnlyCollection<WeeklyChallengeId> ids, CancellationToken cancellationToken)
        {
            foreach (var challenge in _challenges.Where(challenge => ids.Contains(challenge.Id!) && challenge.Status == ChallengeStatus.Active))
                challenge.Cancel();

            return Task.CompletedTask;
        }

        public Task MarkSeenAsync(UserId userId, IReadOnlyCollection<WeeklyChallengeId> ids, DateTimeOffset now, CancellationToken cancellationToken)
        {
            foreach (var challenge in _challenges.Where(challenge => challenge.UserId == userId && challenge.SeenAt is null && ids.Contains(challenge.Id!)))
                challenge.MarkSeen(now);

            return Task.CompletedTask;
        }

        private int LockProfile(UserId userId)
        {
            var index = _profiles.FindIndex(profile => profile.UserId == userId);
            if (index >= 0)
                return index;

            var newProfile = PlayerProfile.CreateDefault(userId);
            newProfile.AssignId(_profiles.Count + 1);
            _profiles.Add(newProfile);

            return _profiles.Count - 1;
        }

        private static IReadOnlyList<WeeklyChallenge> Copies(IEnumerable<WeeklyChallenge> challenges)
            => [.. challenges.Select(Copy)];

        private static WeeklyChallenge Copy(WeeklyChallenge challenge)
            => WeeklyChallenge.FromPersistence(
                challenge.Id!.Value, challenge.UserId.Value, challenge.HabitId.Value, challenge.WeekStart, challenge.Type, challenge.Target,
                challenge.Baseline, challenge.Effort, challenge.RewardCoins, challenge.RewardXp, challenge.Status, challenge.CreatedAt,
                challenge.CompletedAt, challenge.SeenAt, challenge.FinalProgress);

        private static PlayerProfile CopyProfile(PlayerProfile profile)
            => PlayerProfile.FromPersistence(
                profile.Id!.Value, profile.UserId.Value, profile.TotalXp, profile.Hp, profile.MaxHp, profile.Coins,
                profile.StreakFreezes, profile.Avatar, profile.Title);
    }
}
