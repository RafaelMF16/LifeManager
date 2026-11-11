using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Users.Services
{
    /// <summary>Deletes for good the accounts whose grace period is over (the hourly <c>AccountDeletionRunner</c>).</summary>
    public class AccountDeletionService(IAccountDeletionRepository accountDeletionRepository, TimeProvider timeProvider)
    {
        /// <summary>How many accounts one run reads; the rest wait for the next run.</summary>
        public const int DueBatchSize = 100;

        private readonly IAccountDeletionRepository _accountDeletionRepository = accountDeletionRepository;
        private readonly TimeProvider _timeProvider = timeProvider;

        public Task<IReadOnlyList<UserId>> GetDueUserIdsAsync(CancellationToken cancellationToken)
            => _accountDeletionRepository.GetDueUserIdsAsync(_timeProvider.GetUtcNow(), DueBatchSize, cancellationToken);

        /// <summary>True when the account was deleted; false when its deletion was cancelled meanwhile.</summary>
        public Task<bool> DeleteIfDueAsync(UserId userId, CancellationToken cancellationToken)
            => _accountDeletionRepository.DeleteIfDueAsync(userId, _timeProvider.GetUtcNow(), cancellationToken);
    }
}
