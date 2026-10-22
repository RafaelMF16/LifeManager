using LifeManager.Application.StatementImports.Services;

namespace LifeManager.WebApi.StatementImports.Jobs
{
    /// <summary>
    /// One run of the statement imports cleanup: deletes the drafts nobody confirmed within
    /// <see cref="Domain.StatementImports.StatementImport.DraftRetentionDays"/> days. Triggered every hour by
    /// <see cref="StatementImportsCleanupJob"/> (local) or by the <c>--run-jobs</c> mode (Cloud Run Job); running it more
    /// often than needed only finds nothing to delete.
    /// </summary>
    public sealed class StatementImportsCleanupRunner(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<StatementImportsCleanupRunner> logger)
    {
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly ILogger<StatementImportsCleanupRunner> _logger = logger;

        public async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                // The runner is a singleton and the DbContext is scoped, so the run gets its own scope.
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var statementImportService = scope.ServiceProvider.GetRequiredService<StatementImportService>();

                var deleted = await statementImportService.DeleteStaleDraftsAsync(stoppingToken);
                if (deleted > 0)
                    _logger.LogInformation("Deleted {Count} stale statement import draft(s)", deleted);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Could not delete the stale statement import drafts; retrying on the next run");
            }
        }
    }
}
