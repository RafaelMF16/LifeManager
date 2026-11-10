using LifeManager.Application.Users.Services;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.WebApi.Users.Jobs
{
    /// <summary>
    /// One run of the account deletion (<see cref="AccountDeletionService"/>): every account whose grace period is over,
    /// each in its own scope, a failure logged and retried on the next run. Triggered every hour by
    /// <see cref="AccountDeletionJob"/> (local) or by the <c>--run-jobs</c> mode (Cloud Run Job).
    /// </summary>
    public sealed class AccountDeletionRunner(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<AccountDeletionRunner> logger)
    {
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly ILogger<AccountDeletionRunner> _logger = logger;

        public async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            IReadOnlyList<UserId> dueIds;

            try
            {
                // The runner is a singleton and the DbContext is scoped, so every unit of work gets its own scope.
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                dueIds = await scope.ServiceProvider.GetRequiredService<AccountDeletionService>().GetDueUserIdsAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Could not read the accounts due for deletion; retrying on the next run");
                return;
            }

            foreach (var userId in dueIds)
                await DeleteAsync(userId, stoppingToken);
        }

        private async Task DeleteAsync(UserId userId, CancellationToken stoppingToken)
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var deleted = await scope.ServiceProvider.GetRequiredService<AccountDeletionService>().DeleteIfDueAsync(userId, stoppingToken);

                if (deleted)
                    _logger.LogInformation("Deleted account {UserId} after its grace period", userId.Value);
                else
                    _logger.LogInformation("Account {UserId} was not deleted: its deletion was cancelled", userId.Value);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Failed to delete account {UserId}; retrying on the next run", userId.Value);
            }
        }
    }
}
