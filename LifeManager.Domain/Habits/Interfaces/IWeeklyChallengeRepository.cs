using LifeManager.Domain.Habits.Challenges;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits.Interfaces
{
    public interface IWeeklyChallengeRepository
    {
        /// <summary>The user's challenges of the week (any status), oldest first.</summary>
        Task<IReadOnlyList<WeeklyChallenge>> GetByWeekAsync(UserId userId, DateOnly weekStart, CancellationToken cancellationToken);

        /// <summary>The user's challenges still <see cref="ChallengeStatus.Active"/>, of any week.</summary>
        Task<IReadOnlyList<WeeklyChallenge>> GetActiveAsync(UserId userId, CancellationToken cancellationToken);

        Task<WeeklyChallenge?> GetByIdAsync(WeeklyChallengeId id, UserId userId, CancellationToken cancellationToken);

        /// <summary>The challenges of the weeks before <paramref name="beforeWeek"/>, cancelled ones left out, newest week first.</summary>
        Task<PagedList<WeeklyChallenge>> GetHistoryAsync(UserId userId, DateOnly beforeWeek, PageRequest pageRequest, CancellationToken cancellationToken);

        /// <summary>Completed challenges the player hasn't seen celebrated yet, oldest first.</summary>
        Task<IReadOnlyList<WeeklyChallenge>> GetUnseenCompletedAsync(UserId userId, CancellationToken cancellationToken);

        /// <summary>
        /// Under the profile's lock, checks the week's limits against the stored challenges (cancelled ones left out) and
        /// stores <paramref name="challenge"/>.
        /// </summary>
        /// <returns>
        /// The new id, or <see cref="Errors.ChallengeErrors.AlreadyExists"/> / <see cref="Errors.ChallengeErrors.WeekLimitReached"/>
        /// with nothing written.
        /// </returns>
        Task<Result<WeeklyChallengeId>> AddAsync(WeeklyChallenge challenge, CancellationToken cancellationToken);

        /// <summary>Deletes the challenge if it is still active.</summary>
        /// <returns>Whether it was deleted.</returns>
        Task<bool> DeleteActiveAsync(WeeklyChallengeId id, UserId userId, CancellationToken cancellationToken);

        /// <summary>
        /// In one transaction under the profile's lock: moves each challenge from active to completed with its progress
        /// (only if it still is, so it pays once) and stores what <paramref name="pay"/> applies to the profile for it.
        /// </summary>
        /// <returns>The challenges this call completed.</returns>
        Task<IReadOnlyList<WeeklyChallenge>> CompleteAsync(
            UserId userId,
            IReadOnlyCollection<(WeeklyChallenge Challenge, int Progress)> challenges,
            DateTimeOffset completedAt,
            Func<PlayerProfile, WeeklyChallenge, IReadOnlyList<GameLedgerEntry>> pay,
            CancellationToken cancellationToken);

        /// <summary>Fails the challenges still active, storing the progress each one ended with.</summary>
        Task FailAsync(IReadOnlyCollection<(WeeklyChallengeId Id, int Progress)> challenges, CancellationToken cancellationToken);

        /// <summary>Cancels the challenges still active.</summary>
        Task CancelAsync(IReadOnlyCollection<WeeklyChallengeId> ids, CancellationToken cancellationToken);

        Task MarkSeenAsync(UserId userId, IReadOnlyCollection<WeeklyChallengeId> ids, DateTimeOffset now, CancellationToken cancellationToken);
    }
}
