using LifeManager.Application.Auth.Services;

namespace LifeManager.WebApi.Auth.Jobs
{
    /// <summary>
    /// One run of the auth cleanup (<see cref="AuthCleanupService"/>): expired sign-ups, stale reset links and old e-mail
    /// cooldowns. Triggered every hour by <see cref="AuthCleanupJob"/> (local) or by the <c>--run-jobs</c> mode (Cloud Run
    /// Job); running it more often than needed only finds nothing to delete.
    /// </summary>
    public sealed class AuthCleanupRunner(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<AuthCleanupRunner> logger)
    {
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly ILogger<AuthCleanupRunner> _logger = logger;

        public async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                // The runner is a singleton and the DbContext is scoped, so the run gets its own scope.
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var authCleanupService = scope.ServiceProvider.GetRequiredService<AuthCleanupService>();

                var deleted = await authCleanupService.DeleteStaleAsync(stoppingToken);
                if (deleted.Total > 0)
                    _logger.LogInformation(
                        "Deleted {PendingRegistrations} expired sign-up(s), {PasswordResetTokens} stale reset link(s) and {EmailCooldowns} old e-mail cooldown(s)",
                        deleted.PendingRegistrations, deleted.PasswordResetTokens, deleted.EmailCooldowns);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Could not run the auth cleanup; retrying on the next run");
            }
        }
    }
}
